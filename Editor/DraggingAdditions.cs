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
				var imguiContainer = visualTree.Q<IMGUIContainer>();
				imguiContainer.UnregisterCallback<DragEnterEvent>(DragEnter);
				imguiContainer.UnregisterCallback<DragUpdatedEvent, (Object, IMGUIContainer)>(DragUpdated);
				imguiContainer.UnregisterCallback<DragLeaveEvent>(DragLeave);
				
				imguiContainer.RegisterCallback<DragEnterEvent>(DragEnter);
				imguiContainer.RegisterCallback<DragUpdatedEvent, (Object, IMGUIContainer)>(DragUpdated, (dockArea, imguiContainer));
				imguiContainer.RegisterCallback<DragLeaveEvent>(DragLeave);
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
		
		#region Callbacks
		private static long hoverTargetTime;
		private static Vector2 enterMousePosition;
		private const long hoverTime = 250L;

		private static void DragEnter(DragEnterEvent evt)
		{
			hoverTargetTime = evt.timestamp + hoverTime;
			enterMousePosition = evt.mousePosition;
		}

		private static void DragUpdated(DragUpdatedEvent evt, (Object dockArea, IMGUIContainer container) args)
		{
			Object dockArea = args.dockArea;
			Rect r = args.container.contentRect;
			//Ensure that the cursor is in the header.
			Rect contentRect = new Rect(r.x, r.y, r.width - 50, 20);
			if (!contentRect.Contains(evt.mousePosition))
				return;
			
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

			if (time <= hoverTargetTime)
				return;
			
			hoverTargetTime = -2;
			PropertyInfo selectedPI = dockAreaType.GetProperty("selected", BindingFlags.Public | BindingFlags.Instance);
			MethodInfo getTabAtMousePosMI = dockAreaType.GetMethod("GetTabAtMousePos", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] {typeof(GUIStyle), typeof(Vector2), typeof(Rect)}, null);

			selectedPI.SetValue(dockArea, getTabAtMousePosMI.Invoke(dockArea, new object[] {new GUIStyle("dragtab"), evt.mousePosition, contentRect}));
		}

		private static void DragLeave(DragLeaveEvent evt) => hoverTargetTime = -2;
		#endregion
	}
}