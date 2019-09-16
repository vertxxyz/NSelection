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
		static DraggingAdditions() => EditorApplication.delayCall += Delayed;

		private static Type dockAreaType;

		static void Delayed()
		{
			dockAreaType = Type.GetType("UnityEditor.DockArea, UnityEditor");
			Type guiViewType = Type.GetType("UnityEditor.GUIView, UnityEditor");
			Type panelType = Type.GetType("UnityEngine.UIElements.Panel, UnityEngine");

			if (dockAreaType == null || guiViewType == null)
			{
				Debug.LogWarning($"{nameof(DraggingAdditions)} is not compatible with this Unity version. Either see if there is an update, or remove it from your project.");
				return;
			}

			PropertyInfo panelPI = guiViewType.GetProperty("panel", BindingFlags.NonPublic | BindingFlags.Instance);
			PropertyInfo visualTreePI = panelType.GetProperty("visualTree", BindingFlags.Public | BindingFlags.Instance);
			
			if (panelPI == null || visualTreePI == null)
			{
				Debug.LogWarning($"{nameof(DraggingAdditions)} is not compatible with this Unity version. Either see if there is an update, or remove it from your project.");
				return;
			}

			Object[] dockAreas = Resources.FindObjectsOfTypeAll(dockAreaType);
			foreach (Object dockArea in dockAreas)
			{
				object panel = panelPI.GetValue(dockArea);
				VisualElement visualTree = (VisualElement) visualTreePI.GetValue(panel);
				visualTree.Q<IMGUIContainer>().Add(new DragReceiver(dockArea));
//				visualTree.Add(new DragReceiver(dockArea));
			}
		}

		private class DragReceiver : VisualElement
		{
			public Object DockArea { get; }

			public DragReceiver(Object dockArea)
			{
				DockArea = dockArea;
				style.height = 20;
				style.minHeight = 20;
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


					/*//This code removes the drag and drop info and fails to work.
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

				if (evt.eventTypeId == ContextClickEvent.TypeId())
				{
					Event e = new Event
					{
						type = evt.imguiEvent.type,
						button = evt.imguiEvent.button,
						clickCount = evt.imguiEvent.clickCount,
						mousePosition = evt.imguiEvent.mousePosition,
						pointerType = evt.imguiEvent.pointerType,
						delta = evt.imguiEvent.delta
					};
					using (MouseDownEvent mouseDownEvent = MouseDownEvent.GetPooled(e))
					{
						mouseDownEvent.target = parent;
						parent.SendEvent(mouseDownEvent);
					}

					return;
				}
				
				/*if (evt.eventTypeId == MouseDownEvent.TypeId()
				    /*|| evt.eventTypeId == PointerDownEvent.TypeId()#1#)
				{
					Event e = new Event
					{
						type = evt.imguiEvent.type,
						button = evt.imguiEvent.button,
						clickCount = evt.imguiEvent.clickCount,
						mousePosition = evt.imguiEvent.mousePosition,
						pointerType = evt.imguiEvent.pointerType,
						delta = evt.imguiEvent.delta
					};
					using (MouseDownEvent mouseDownEvent = MouseDownEvent.GetPooled(e))
					{
						mouseDownEvent.target = parent;
						parent.SendEvent(mouseDownEvent);
					}

					return;
				}

				if (evt.eventTypeId == MouseUpEvent.TypeId()
					/*|| evt.eventTypeId == PointerUpEvent.TypeId()#1#)
				{
					Event e = new Event
					{
						type = evt.imguiEvent.type,
						button = evt.imguiEvent.button,
						clickCount = evt.imguiEvent.clickCount,
						mousePosition = evt.imguiEvent.mousePosition,
						pointerType = evt.imguiEvent.pointerType,
						delta = evt.imguiEvent.delta
					};
					using (MouseUpEvent mouseUpEvent = MouseUpEvent.GetPooled(e))
					{
						Debug.Log(mouseUpEvent);
						mouseUpEvent.target = parent;
						parent.SendEvent(mouseUpEvent);
					}
				}*/

				/*if (evt.eventTypeId == MouseMoveEvent.TypeId() || evt.eventTypeId == PointerMoveEvent.TypeId())
					return;
				if(evt.imguiEvent != null)
					Debug.Log($"{evt} {evt.imguiEvent?.type}");*/
				base.ExecuteDefaultAction(evt);
			}
		}
	}
}