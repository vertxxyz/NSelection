using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Profiling.Editor;
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
		/// The supported windows are: Hierarchy view, Project browser, Timeline window, and the Animation window.
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
						// Focus project browser.
						FocusProjectBrowserToSelection(focusedWindow);
						return;
					case "UnityEditor.ProfilerWindow":
						// Focus profiler window.
						FocusProfilerWindowToSelection((ProfilerWindow)focusedWindow);
						return;
					case "UnityEditor.AnimationWindow":
						// Focus animation window.
						FocusAnimationWindowToSelection((AnimationWindow)focusedWindow);
						return;
#if UNITY_TIMELINE
					case "UnityEditor.Timeline.TimelineWindow":
						// Focus timeline window.
						FocusTimelineWindowToSelection();
						return;
#endif
					case "UnityEditor.UIElements.Debugger.UIElementsDebugger":
						// Focus UIToolkit debugger
						FocusUIToolkitDebugger(focusedWindow);
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
		public static void FocusProfilerWindowToSelection(ProfilerWindow profilerWindow)
		{
			var interfaceType = Type.GetType("UnityEditorInternal.IProfilerWindowController,UnityEditor");
			PropertyInfo selectedModuleProperty = interfaceType.GetProperty("selectedModule", BindingFlags.Public | BindingFlags.Instance);
			ProfilerModule module = (ProfilerModule)selectedModuleProperty.GetValue(profilerWindow);
			
			// CPUOrGPUProfilerModule.FrameDataHierarchyView
			PropertyInfo frameDataHierarchyViewProperty = module.GetType()
				.GetProperty("FrameDataHierarchyView", BindingFlags.NonPublic | BindingFlags.Instance);
			if (frameDataHierarchyViewProperty != null)
			{
				// ProfilerFrameDataHierarchyView
				object frameDataHierarchyView = frameDataHierarchyViewProperty.GetValue(module);
				TreeView treeView = (TreeView)frameDataHierarchyView.GetType().GetProperty("treeView", BindingFlags.Public | BindingFlags.Instance).GetValue(frameDataHierarchyView);
				treeView.state.expandedIDs = new List<int>();
				treeView.SetSelection(treeView.state.selectedIDs, TreeViewSelectionOptions.RevealAndFrame);
				treeView.Reload();
			}
		}

		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusProjectBrowserToSelection(EditorWindow projectBrowser) => FocusGenericHierarchyWithField(projectBrowser, "m_FolderTree");
		
		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusAnimationWindowToSelection(AnimationWindow animationWindow)
		{
			object animEditor = animationWindow.GetType().GetProperty("animEditor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(animationWindow);
			object animationWindowHierarchy = animEditor.GetType().GetField("m_Hierarchy", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(animEditor);
			FocusGenericHierarchyWithField(animationWindowHierarchy, "m_TreeView");
			
			// TODO make this not have to be a toggle, it's annoying broken right now.
		}
		
		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusUIToolkitDebugger(EditorWindow uitoolkitDebugger)
		{
			object debuggerImpl = uitoolkitDebugger.GetType().GetProperty("debuggerImpl", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(uitoolkitDebugger);
			object treeViewContainer = debuggerImpl.GetType().GetField("m_TreeViewContainer", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(debuggerImpl);
			var treeView = (UIToolkit.TreeView)treeViewContainer.GetType().GetField("m_TreeView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(treeViewContainer);
			HashSet<int> parents = new HashSet<int>();
			foreach (int selectedIndex in treeView.selectedIndices)
			{
				int parentId = treeView.GetParentIdForIndex(selectedIndex);
				if (parentId >= 0)
					CollectParents(treeView.viewController, parentId, parents);
			}

			treeView.CollapseAll();
			Debug.Log(parents.Count);
			foreach (int parent in parents)
				treeView.ExpandItem(parent);
			treeView.Rebuild();
			
			// TODO support expanding to see selection.
		}

		private static void CollectParents(UIToolkit.TreeViewController viewController, int id, HashSet<int> parents)
		{
			while (true)
			{
				if (!parents.Add(id))
					return;
				id = viewController.GetParentId(id);
				if (id < 0)
					return;
			}
		}
	}
}