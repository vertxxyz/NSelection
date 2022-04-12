#if !DISABLE_DRAGGING_ADDITIONS
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif
using Object = UnityEngine.Object;

namespace Vertx
{
	[InitializeOnLoad]
	internal static class DraggingAdditions
	{
		static DraggingAdditions() => EditorApplication.delayCall += Initialise;

		private static Type dockAreaType;
		private static Type guiViewType;
		#if UNITY_2020_1_OR_NEWER
		private static Type iWindowBackendType;
		private static PropertyInfo windowBackendPI;
		#else
		private static Type panelType;
		private static PropertyInfo panelPI;
		#endif
		private static PropertyInfo visualTreePI;
		private static bool initialised;
		private const float refreshTime = 20;
		private static float waitToTime;

		//Types
		private const string
			k_DockArea = "UnityEditor.DockArea",
			k_GUIView = "UnityEditor.GUIView",
			#if UNITY_2020_1_OR_NEWER
			k_IWindowBackend = "UnityEditor.IWindowBackend";
			#elif UNITY_2019_1_OR_NEWER
			k_Panel = "UnityEngine.UIElements.Panel";
		#else
			k_Panel = "UnityEngine.Experimental.UIElements.Panel";
		#endif

		//Properties
		private const string
			#if UNITY_2020_1_OR_NEWER
			k_WindowBackend = "windowBackend",
			k_WindowBackendVisualTree = "visualTree";
			#else
			k_GUIViewPanel = "panel",
			k_PanelVisualTree = "visualTree";
		#endif

		static void Initialise()
		{
			if (!initialised)
			{
				Assembly editorAssembly = typeof(EditorWindow).Assembly;
				//Dock Area
				dockAreaType = editorAssembly.GetType(k_DockArea);
				if (ShowNotCompatibleError(dockAreaType, $"Type {k_DockArea} was not found"))
					return;

				//GUIView
				guiViewType = editorAssembly.GetType(k_GUIView);
				if (ShowNotCompatibleError(guiViewType, $"Type {k_GUIView} was not found"))
					return;


				#if UNITY_2020_1_OR_NEWER
				//Retrieving the visual tree (VisualElement) from Window Backend in GUIView
				windowBackendPI = guiViewType.GetProperty(k_WindowBackend, BindingFlags.NonPublic | BindingFlags.Instance);
				if (ShowNotCompatibleError(windowBackendPI, $"Property {k_WindowBackend} was not found on {guiViewType}"))
					return;
				
				iWindowBackendType = editorAssembly.GetType(k_IWindowBackend);
				if (ShowNotCompatibleError(iWindowBackendType, $"Type {k_IWindowBackend} was not found"))
					return;

				visualTreePI = iWindowBackendType.GetProperty(k_WindowBackendVisualTree, BindingFlags.Public | BindingFlags.Instance);
				if (ShowNotCompatibleError(visualTreePI, $"Property {k_WindowBackendVisualTree} was not found on {iWindowBackendType}"))
					return;
				#else
				//Retrieving the visual tree (VisualElement) from Panel in GUIView
				Assembly uiElementsAssembly = typeof(VisualElement).Assembly;
				panelType = uiElementsAssembly.GetType(k_Panel);
				if (ShowNotCompatibleError(panelType, $"Type {k_Panel} was not found"))
					return;
				panelPI = guiViewType.GetProperty(k_GUIViewPanel, BindingFlags.NonPublic | BindingFlags.Instance);
				visualTreePI = panelType.GetProperty(k_PanelVisualTree, BindingFlags.Public | BindingFlags.Instance);

				if (ShowNotCompatibleError(panelPI, $"Property {k_GUIViewPanel} was not found on {guiViewType}"))
					return;
				if (ShowNotCompatibleError(panelPI, $"Property {k_PanelVisualTree} was not found on {panelType}"))
					return;
				#endif
			}

			Object[] dockAreas = Resources.FindObjectsOfTypeAll(dockAreaType);
			foreach (Object dockArea in dockAreas)
			{
				//Inject callbacks into the dock area visual elements.
				#if UNITY_2020_1_OR_NEWER
				object windowBackend = windowBackendPI.GetValue(dockArea);
				VisualElement visualTree = (VisualElement) visualTreePI.GetValue(windowBackend);
				#else
				object panel = panelPI.GetValue(dockArea);
				VisualElement visualTree = (VisualElement) visualTreePI.GetValue(panel);
				#endif
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

		/// <summary>
		/// Shows the warning informing the user that this in incompatible
		/// </summary>
		/// <param name="query">The object to query for null</param>
		/// <param name="reason">Optional reason for incompatibility</param>
		/// <returns>True if incompatible</returns>
		private static bool ShowNotCompatibleError(object query, string reason = null)
		{
			if (query != null)
				return false;
			var extraMessage = string.IsNullOrEmpty(reason) ? string.Empty : $", because {reason}";
			Debug.LogWarning(
				$"{nameof(DraggingAdditions)} is not compatible with this Unity version{extraMessage}.\n" +
				"Either see if there is an update, add \"DISABLE_DRAGGING_ADDITIONS\" to your Scripting Define Symbols, or remove it from your project.");
			return true;
		}

		private static void Update()
		{
			float updateTime = Time.realtimeSinceStartup;
			if (waitToTime > updateTime)
				return;

			//Only reinitialise if the application has focus.
			if(UnityEditorInternal.InternalEditorUtility.isApplicationActive)
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
			MethodInfo getTabAtMousePosMI = dockAreaType.GetMethod("GetTabAtMousePos", BindingFlags.NonPublic | BindingFlags.Instance, null,
				new[] {typeof(GUIStyle), typeof(Vector2), typeof(Rect)}, null);

			selectedPI.SetValue(dockArea, getTabAtMousePosMI.Invoke(dockArea, new object[] {new GUIStyle("dragtab"), evt.mousePosition, contentRect}));
		}

		private static void DragLeave(DragLeaveEvent evt) => hoverTargetTime = -2;

		#endregion
	}
}
#endif