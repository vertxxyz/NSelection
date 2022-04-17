using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using Object = UnityEngine.Object;
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
		/// Hierarchy view, Project browser, Profiler window, Timeline window, Animation window, Audio Mixer, UIToolkit debugger, and Frame debugger
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
				}

				// Debug.Log(focusedWindowTypeName);
			}

			// Focus hierarchy by default.
			FocusHierarchyViewToSelection();
		}

		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusHierarchyViewToSelection()
		{
			if (HierarchyWindow == null)
				return;

			object sceneHierarchy = SceneHierarchy;
			FocusGenericHierarchyWithProperty(sceneHierarchy, "treeView");
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
			Type uiToolkitDebuggerType = uitoolkitDebugger.GetType();
			PropertyInfo debuggerImplProperty = uiToolkitDebuggerType.GetProperty("debuggerImpl", NonPublicInstance);

			object treeViewContainer;
			if (debuggerImplProperty != null)
			{
				object debuggerImpl =
					debuggerImplProperty.GetValue(uitoolkitDebugger);

				treeViewContainer = debuggerImpl.GetType().GetField("m_TreeViewContainer", NonPublicInstance)
					.GetValue(debuggerImpl);
			}
			else
			{
				treeViewContainer = uiToolkitDebuggerType.GetField("m_TreeViewContainer", NonPublicInstance)
					.GetValue(uitoolkitDebugger);
			}

			Type treeViewContainerType = treeViewContainer.GetType();
#if UNITY_2022_1_OR_NEWER
			var treeView = (UIToolkit.TreeView)treeViewContainerType.GetField("m_TreeView", NonPublicInstance)
				.GetValue(treeViewContainer);

			treeView.CollapseAll();
#else
			object treeView = treeViewContainerType.GetField("m_TreeView", NonPublicInstance)
				.GetValue(treeViewContainer);

			Type treeViewType = treeView.GetType();
			MethodInfo collapseAllMethod = treeViewType.GetMethod("CollapseAll", PublicInstance);
			if (collapseAllMethod != null)
				collapseAllMethod.Invoke(treeView, null);
			else
			{
				var expandedIds =
					(List<int>)treeViewType.GetField("m_ExpandedItemIds", NonPublicInstance).GetValue(treeView);
				expandedIds.Clear();
			}
#endif

			object debuggerSelection = treeViewContainerType.GetField("m_DebuggerSelection", NonPublicInstance)
				.GetValue(treeViewContainer);

			Type debuggerSelectionType = debuggerSelection.GetType();
			PropertyInfo selectedElement = debuggerSelectionType.GetProperty("element", PublicInstance);
			object selection = selectedElement.GetValue(debuggerSelection);
			debuggerSelectionType.GetField("m_Element", NonPublicInstance).SetValue(debuggerSelection, null);
			selectedElement.SetValue(debuggerSelection, selection);
		}

		/// <summary>
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
					"m_AudioGroupTree");
			}
		}
	}
}