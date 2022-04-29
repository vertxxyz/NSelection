using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using TreeView = UnityEditor.IMGUI.Controls.TreeView;
using UIToolkit = UnityEngine.UIElements;

namespace Vertx
{
	public partial class NSelection
	{
		/// <summary>
		/// Collapses everything in the Hierarchy view except the current selection and any uncollapsed scenes.
		/// </summary>
		[Shortcut("Hierarchy View/Collapse Hierarchy")]
		public static void CollapseHierarchy()
		{
			if (HierarchyWindow == null)
				return;
			object sceneHierarchy = SceneHierarchy;

			int[] expandedState = GetHierarchyExpandedState();
			List<int> newState = new List<int>();

			// Collect selection and objects up to the root.
			HashSet<GameObject> selection = new HashSet<GameObject>();
			GameObject[] selectedGameObjects = Selection.gameObjects;
			foreach (GameObject selected in selectedGameObjects)
				CollectParents(selected, selection);

			// Persist the selection in the state.
			foreach (int i in expandedState)
			{
				Object o = HierarchyIdToObject(i, sceneHierarchy);
				// Scenes come through as null. I could improve this to be more accurate, but I'm unsure whether there's a need.
				// Will improve this if bugs are reported.
				if (o == null || o is SceneAsset || selection.Contains(o))
					newState.Add(i);
			}

			SetHierarchyToState(newState, sceneHierarchy);
		}

		/// <summary>
		/// Collapses everything in the Hierarchy view.
		/// </summary>
		[Shortcut("Hierarchy View/Collapse Hierarchy Completely")]
		public static void CollapseHierarchyCompletely()
		{
			if (HierarchyWindow == null)
				return;

			SetHierarchyToState(new List<int>());
		}

		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.<br/>
		/// The supported windows are:
		/// Hierarchy view, Project browser, Profiler window, Timeline window, Animation window, Audio Mixer, UI Builder, UIToolkit debugger, and Frame debugger
		/// </summary>
		[Shortcut("Window/Focus hierarchy to selection", KeyCode.F, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
		public static void FocusCurrentHierarchyToSelection()
		{
			EditorWindow focusedWindow = EditorWindow.focusedWindow;
			if (focusedWindow != null)
			{
				string focusedWindowTypeName = focusedWindow.GetType().FullName;
				switch (focusedWindowTypeName)
				{
					case "UnityEditor.ProjectBrowser":
						FocusProjectBrowserToSelection(focusedWindow);
						return;
					case "UnityEditor.ProfilerWindow":
						FocusProfilerWindowToSelection(focusedWindow);
						return;
					case "UnityEditor.AnimationWindow":
						FocusAnimationWindowToSelection(focusedWindow);
						return;
#if UNITY_TIMELINE
					case "UnityEditor.Timeline.TimelineWindow":
						FocusTimelineWindowToSelection();
						return;
#endif
					case "UnityEditor.UIElements.Debugger.UIElementsDebugger":
						FocusUIToolkitDebuggerToSelection(focusedWindow);
						return;
					case "UnityEditor.FrameDebuggerWindow":
						FocusFrameDebuggerToSelection(focusedWindow);
						return;
					case "UnityEditor.AudioMixerWindow":
						FocusAudioMixerToSelection(focusedWindow);
						return;
					case "Unity.UI.Builder.Builder":
						FocusUIBuilderToSelection(focusedWindow);
						return;
					case "UnityEditor.InspectorWindow":
						FocusInspectorWindowToSelection(focusedWindow);
						return;
					case "UnityEditor.SceneHierarchyWindow":
						FocusHierarchyViewToSelection();
						return;
				}

				// Debug.Log(focusedWindowTypeName);
			}

			// Focus hierarchy by default.
			FocusHierarchyViewToSelection(true);
		}

		/// <summary>
		/// Sets the expanded state of the target hierarchy to only contain the current selection.
		/// The target hierarchy is determined by the current inspector's focused object.
		/// This is either the scene view hierarchy, or the project view.
		/// </summary>
		public static void FocusInspectorWindowToSelection(EditorWindow inspectorWindow)
		{
			object inspectedObject = inspectorWindow.GetType()
				.GetMethod("GetInspectedObject", NonPublicInstance)
				?.Invoke(inspectorWindow, null);
			if (inspectedObject is Object o)
			{
				if (AssetDatabase.Contains(o))
				{
					Object[] projectBrowsers = Resources.FindObjectsOfTypeAll(Type.GetType("UnityEditor.ProjectBrowser,UnityEditor"));
					foreach (Object projectBrowser in projectBrowsers)
					{
						if (!(projectBrowser is EditorWindow editorWindow))
							continue;
						FocusProjectBrowserToSelection(editorWindow);
						editorWindow.Repaint();
					}

					return;
				}
			}

			FocusHierarchyViewToSelection(true);
		}

		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusHierarchyViewToSelection(bool forceRepaint = false)
		{
			if (HierarchyWindow == null)
				return;

			object sceneHierarchy = SceneHierarchy;
			FocusGenericHierarchyWithProperty(sceneHierarchy, "treeView");
			if (forceRepaint)
				HierarchyWindow.Repaint();
		}

		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusProfilerWindowToSelection(EditorWindow profilerWindow)
		{
			var interfaceType = Type.GetType("UnityEditorInternal.IProfilerWindowController,UnityEditor");

			// Get the selected profiler module, with fallbacks.
			PropertyInfo selectedModuleProperty = interfaceType.GetProperty("selectedModule", PublicInstance);
			if (selectedModuleProperty == null)
				selectedModuleProperty = interfaceType.GetProperty("SelectedModule", PublicInstance);

			object module;
			if (selectedModuleProperty == null)
			{
				// m_ProfilerModules[(int) m_CurrentArea].
				Type profilerWindowType = profilerWindow.GetType();
				Array modules = (Array)profilerWindowType.GetField("m_ProfilerModules", NonPublicInstance)
					.GetValue(profilerWindow);
				int index = (int)profilerWindowType.GetField("m_CurrentArea", NonPublicInstance)
					.GetValue(profilerWindow);
				module = modules.GetValue(index);
			}
			else
			{
				module = selectedModuleProperty.GetValue(profilerWindow); // ProfilerModule
			}

			// CPUOrGPUProfilerModule.FrameDataHierarchyView
			PropertyInfo frameDataHierarchyViewProperty = module.GetType()
				.GetProperty("FrameDataHierarchyView", NonPublicInstance);
			if (frameDataHierarchyViewProperty != null)
			{
				// ProfilerFrameDataHierarchyView
				object frameDataHierarchyView = frameDataHierarchyViewProperty.GetValue(module);
				TreeView treeView = (TreeView)frameDataHierarchyView.GetType().GetProperty("treeView", PublicInstance)
					.GetValue(frameDataHierarchyView);
				treeView.state.expandedIDs = new List<int>();
				treeView.SetSelection(treeView.state.selectedIDs, TreeViewSelectionOptions.RevealAndFrame);
				treeView.Reload();
			}
		}

		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusProjectBrowserToSelection(EditorWindow projectBrowser)
		{
			Type windowType = projectBrowser.GetType();
			object folderTree = windowType
				.GetField("m_FolderTree", NonPublicInstance)
				.GetValue(projectBrowser);
			object assetTreeTree = windowType
				.GetField("m_AssetTree", NonPublicInstance)
				.GetValue(projectBrowser);

			if (folderTree != null)
				FocusGenericHierarchy(folderTree);

			if (assetTreeTree != null)
				FocusGenericHierarchy(assetTreeTree);
		}

		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusAnimationWindowToSelection(EditorWindow animationWindow)
		{
			object animEditor = animationWindow.GetType().GetProperty("animEditor", NonPublicInstance)
				.GetValue(animationWindow);
			object animationWindowHierarchy =
				animEditor.GetType().GetField("m_Hierarchy", NonPublicInstance).GetValue(animEditor);
			FocusGenericHierarchyWithField(
				animationWindowHierarchy,
				"m_TreeView"
			);
			// TODO include expanding properties that contain selected keys.
		}

		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusUIToolkitDebuggerToSelection(EditorWindow uitoolkitDebugger)
		{
#if UNITY_2022_1_OR_NEWER
			var treeView = (UIToolkit.TreeView)uitoolkitDebugger.rootVisualElement.Q(null, "unity-tree-view");
#else
			object treeView = uitoolkitDebugger.rootVisualElement.Q("unity-tree-view__list-view", "unity-tree-view__list-view").parent;
			;
#endif
			TreeViewFocusSelection(treeView);
		}

		/// <summary>DD
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusFrameDebuggerToSelection(EditorWindow frameDebugger)
		{
			FieldInfo treeView = frameDebugger.GetType().GetField("m_TreeView", NonPublicInstance);
			if (treeView == null)
				treeView = frameDebugger.GetType().GetField("m_Tree", NonPublicInstance);

			FocusGenericHierarchyWithField(
				treeView.GetValue(frameDebugger),
				"m_TreeView");
		}

		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusAudioMixerToSelection(EditorWindow audioMixerWindow)
		{
			FieldInfo groupTree = audioMixerWindow.GetType().GetField("m_GroupTree", NonPublicInstance);
			if (groupTree != null)
			{
				FocusGenericHierarchyWithField(
					groupTree.GetValue(audioMixerWindow),
					"m_AudioGroupTree");
			}

			object mixerTree = audioMixerWindow.GetType().GetField("m_MixersTree", NonPublicInstance)
				.GetValue(audioMixerWindow);
			if (mixerTree != null)
			{
				FocusGenericHierarchyWithField(
					mixerTree,
					"m_TreeView");
			}
		}

		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusUIBuilderToSelection(EditorWindow uiBuilder)
		{
#if UNITY_2022_1_OR_NEWER
			var treeView = (UIToolkit.TreeView)uiBuilder.rootVisualElement.Q("hierarchy").Q("explorer-container")[0];
#else
			object treeView = uiBuilder.rootVisualElement.Q("hierarchy").Q("explorer-container")[0];
#endif
			TreeViewFocusSelection(treeView);
		}

#if UNITY_2022_1_OR_NEWER
		private static void TreeViewFocusSelection(UIToolkit.TreeView treeView)
		{
			int[] selection =
				((List<int>)typeof(UIToolkit.TreeView)
					.GetProperty("currentSelectionIds", NonPublicInstance)
					.GetValue(treeView)).ToArray();
			treeView.ClearSelection();
			treeView.CollapseAll();
			treeView.SetSelectionById(selection);
		}
#else
		private static void TreeViewFocusSelection(object treeView)
		{
			var treeViewItemType = Type.GetType("UnityEngine.UIElements.ITreeViewItem,UnityEngine.UIElementsModule");
			// Collect parents to expand.
			PropertyInfo idProperty = treeViewItemType.GetProperty("id", PublicInstance);
			PropertyInfo parentProperty = treeViewItemType.GetProperty("parent", PublicInstance);
			Type treeViewType = treeView.GetType();
			IEnumerable selection = (IEnumerable)treeViewType.GetProperty(
#if UNITY_2020_1_OR_NEWER
				"selectedItems",
#else
				"currentSelection",
#endif
				PublicInstance
			).GetValue(treeView);

			// Collect parents from selection.
			HashSet<int> parentIds = new HashSet<int>();
			foreach (object o in selection)
			{
				for (object parent = parentProperty.GetValue(o);
				     parent != null;
				     parent = parentProperty.GetValue(parent))
				{
					if (!parentIds.Add((int)idProperty.GetValue(parent)))
						break;
				}
			}

			var expandedIds =
				(List<int>)treeViewType.GetField("m_ExpandedItemIds", NonPublicInstance).GetValue(treeView);
			expandedIds.Clear();
			expandedIds.AddRange(parentIds);
			expandedIds.Sort();

			treeViewType.GetMethod("Refresh", PublicInstance).Invoke(treeView, null);
		}
#endif
	}
}