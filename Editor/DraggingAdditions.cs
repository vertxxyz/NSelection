#define VERBOSE_LOGGING

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Vertx
{
	[InitializeOnLoad]
	public static class DraggingAdditions
	{
		static DraggingAdditions() => EditorApplication.delayCall += Initialise;

		private static Type dockAreaType;
		private static Type guiViewType;
		private static Type panelType;
		private static PropertyInfo panelPI;
		private static PropertyInfo visualTreePI;
		private static bool initialised;
		private const float refreshTime = 20;
		private static float waitToTime;

		static void Initialise()
		{
			if (!initialised)
			{
				dockAreaType = Type.GetType("UnityEditor.DockArea, UnityEditor");
				guiViewType = Type.GetType("UnityEditor.GUIView, UnityEditor");
				panelType = Type.GetType("UnityEngine.UIElements.Panel, UnityEngine");

				if (dockAreaType == null || guiViewType == null)
				{
					Debug.LogWarning($"{nameof(DraggingAdditions)} is not compatible with this Unity version. Either see if there is an update, or remove it from your project.");
					return;
				}

				panelPI = guiViewType.GetProperty("panel", BindingFlags.NonPublic | BindingFlags.Instance);
				visualTreePI = panelType.GetProperty("visualTree", BindingFlags.Public | BindingFlags.Instance);

				if (panelPI == null || visualTreePI == null)
				{
					Debug.LogWarning($"{nameof(DraggingAdditions)} is not compatible with this Unity version. Either see if there is an update, or remove it from your project.");
					return;
				}
			}

			Object[] dockAreas = Resources.FindObjectsOfTypeAll(dockAreaType);
			foreach (Object dockArea in dockAreas)
			{
				object panel = panelPI.GetValue(dockArea);
				VisualElement visualTree = (VisualElement) visualTreePI.GetValue(panel);
				if (visualTree.Q<DragReceiver>() == null)
				{
					visualTree.Add(new DragReceiver(dockArea, visualTree.Q<IMGUIContainer>()));
				}
			}

			if (initialised)
				return;

			initialised = true;
			waitToTime = Time.realtimeSinceStartup + refreshTime;
			EditorApplication.update += Update;
		}

		private static void Update()
		{
			float updateTime = Time.realtimeSinceStartup;
			if (waitToTime > updateTime)
				return;

			Initialise();

			waitToTime = updateTime + refreshTime;
		}

		private class DragReceiver : VisualElement
		{
			public Object DockArea { get; }
			private readonly IMGUIContainer imguiContainer;

			public DragReceiver(Object dockArea, IMGUIContainer imguiContainer)
			{
				this.imguiContainer = imguiContainer;
				DockArea = dockArea;
				style.height = 20;
				style.minHeight = 20;
				style.right = 50;
			}

			private long hoverTargetTime;
			private Vector2 enterMousePosition;
			private const long hoverTime = 250L;

			private void DragEnter(DragEnterEvent evt)
			{
				hoverTargetTime = evt.timestamp + hoverTime;
				enterMousePosition = evt.mousePosition;
			}

			private void DragUpdated(DragUpdatedEvent evt)
			{
				long time = evt.timestamp;

				Vector2 mousePosition = evt.mousePosition;
				if ((mousePosition - enterMousePosition).sqrMagnitude > 100)
				{
					enterMousePosition = mousePosition;
					hoverTargetTime = time + hoverTime;
					return;
				}

				long diff = Math.Abs(hoverTargetTime - time);
				if (diff > 2000L)
				{
					//If the time difference between the drag entering and the current time is too large, then this either hasn't initialised, or something else is wrong.
					return;
				}

				if (time > hoverTargetTime)
				{
					hoverTargetTime = -2;
					PropertyInfo selectedPI = dockAreaType.GetProperty("selected", BindingFlags.Public | BindingFlags.Instance);
					MethodInfo getTabAtMousePosMI = dockAreaType.GetMethod("GetTabAtMousePos", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] {typeof(GUIStyle), typeof(Vector2), typeof(Rect)}, null);

					selectedPI.SetValue(DockArea, getTabAtMousePosMI.Invoke(DockArea, new object[] {new GUIStyle("dragtab"), evt.mousePosition, contentRect}));


					/*
					 * //This code removes the drag and drop info and fails to work.
					 * //Keeping it here for reference purposes
					 var e = new Event{
						type = EventType.MouseDown,
						mousePosition = evt.mousePosition,
						button = 0
					};
					using (MouseDownEvent mouseDownEvent = MouseDownEvent.GetPooled(e))
					{
						parent.SendEvent(mouseDownEvent);
					}*/

					return;
				}
			}

			private void DragLeave(DragLeaveEvent evt) => hoverTargetTime = -2;

			protected override void ExecuteDefaultAction(EventBase evt)
			{
				if (evt.eventTypeId == DragEnterEvent.TypeId())
				{
					DragEnter((DragEnterEvent) evt);
					return;
				}

				if (evt.eventTypeId == DragUpdatedEvent.TypeId())
				{
					DragUpdated((DragUpdatedEvent) evt);
					return;
				}

				if (evt.eventTypeId == DragLeaveEvent.TypeId())
				{
					DragLeave((DragLeaveEvent) evt);
					return;
				}
				
				//This event always seems to need to be re-synthesised
				if (evt.eventTypeId == ContextClickEvent.TypeId())
				{
					Event e = GetEventFromEvt();
					using (MouseDownEvent mouseDownEvent = MouseDownEvent.GetPooled(e))
					{
						mouseDownEvent.target = parent;
						#if VERBOSE_LOGGING
						Debug.Log($"CONTEXT: {mouseDownEvent}");
						#endif
						parent.SendEvent(mouseDownEvent);
					}
					base.ExecuteDefaultAction(evt);
					return;
				}

				//The following events need to be re-synthesised if the UIElement Debugger has worked its magic
				//It seems quite difficult to remove the effects of the UIElement Debugger on this VisualElement
				//So far, without launching the debugger, with this commented out, the tool works.
				//Once the debugger inspects a DragReceiver panel they all become solid and stop passing the following events.
				//Once in this state you can't exit the UIElements Debugger (without spawning a new one to set each other's DragReceivers to PickingMode.Ignore
				//Or by resetting the layout to something else.
				//Once in this state, it also seems hard to break out of. Sometimes not even a script reload seems to do it. Argh!
				/*if (evt.eventTypeId == MouseDownEvent.TypeId())
				{
					Event e = GetEventFromEvt();
					using (MouseDownEvent mouseDownEvent = MouseDownEvent.GetPooled(e))
					{
						mouseDownEvent.target = parent;
						#if VERBOSE_LOGGING
						Debug.Log(mouseDownEvent);
						#endif
						parent.SendEvent(mouseDownEvent);
					}
					base.ExecuteDefaultAction(evt);
					return;
				}

				if (evt.eventTypeId == MouseUpEvent.TypeId())
				{
					Event e = GetEventFromEvt();
					using (MouseUpEvent mouseUpEvent = MouseUpEvent.GetPooled(e))
					{
						mouseUpEvent.target = parent;
						#if VERBOSE_LOGGING
						Debug.Log(mouseUpEvent);
						#endif
						parent.SendEvent(mouseUpEvent);
					}
					base.ExecuteDefaultAction(evt);
					return;
				}*/
				
				//These events seem unimportant to the function either way (IMGUIContainer doesn't use them)
				/*if (evt.eventTypeId == PointerDownEvent.TypeId())
				{
					Event e = GetEventFromEvt();
					using (PointerDownEvent pointerDownEvent = PointerDownEvent.GetPooled(e))
					{
						pointerDownEvent.target = parent;
						#if VERBOSE_LOGGING
						Debug.Log(pointerDownEvent);
						#endif
						parent.SendEvent(pointerDownEvent);
					}
					base.ExecuteDefaultAction(evt);
					return;
				}

				if (evt.eventTypeId == PointerUpEvent.TypeId())
				{
					Event e = GetEventFromEvt();
					using (PointerUpEvent pointerUpEvent = PointerUpEvent.GetPooled(e))
					{
						pointerUpEvent.target = parent;
						#if VERBOSE_LOGGING
						Debug.Log(pointerUpEvent);
						#endif
						parent.SendEvent(pointerUpEvent);
					}
					base.ExecuteDefaultAction(evt);
					return;
				}*/

				Event GetEventFromEvt()
				{
					var imguiEvent = evt.imguiEvent;
					return new Event
					{
						type = imguiEvent.type,
						button = imguiEvent.button,
						clickCount = imguiEvent.clickCount,
						mousePosition = imguiEvent.mousePosition,
						pointerType = imguiEvent.pointerType,
						delta = imguiEvent.delta
					};
				}

				/*if (evt.eventTypeId == MouseMoveEvent.TypeId() || evt.eventTypeId == PointerMoveEvent.TypeId())
					return;
				if(evt.imguiEvent != null)
					Debug.Log($"{evt} {evt.imguiEvent?.type}");*/
				base.ExecuteDefaultAction(evt);
			}
		}
	}
}