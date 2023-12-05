﻿/* Thomas Ingram 2018 */

using System;
#if UNITY_2022_2_OR_NEWER
using System.Collections;
#endif
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable ConvertToNullCoalescingCompoundAssignment

namespace Vertx
{
#if !UNITY_2023_3_OR_NEWER
	[InitializeOnLoad]
#endif
	public static partial class NSelection
	{
#if !UNITY_2023_3_OR_NEWER
		static NSelection() => RefreshListeners();

		private static void RefreshListeners()
		{
#if UNITY_2019_1_OR_NEWER
			SceneView.duringSceneGui -= OnSceneGUI;
			SceneView.duringSceneGui += OnSceneGUI;
#else
			SceneView.onSceneGUIDelegate -= OnSceneGUI;
			SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
		}
		
		private static bool _mouseRightIsDownWithoutDrag;
#endif

		private const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;
		private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
		private const BindingFlags NonPublicStatic = BindingFlags.Static | BindingFlags.NonPublic;
		private const BindingFlags PublicStatic = BindingFlags.Static | BindingFlags.Public;
		
#if UNITY_2023_3_OR_NEWER
		private const string NSelectionMenuCommand = nameof(NSelection) + "Command";
		
		[UnityEditor.ShortcutManagement.Shortcut("Scene View/Show Deep Selection Menu", typeof(SceneView), KeyCode.Mouse1, UnityEditor.ShortcutManagement.ShortcutModifiers.Action)]
		static void ShowDeepSelectionMenu(UnityEditor.ShortcutManagement.ShortcutArguments args)
		{
			if (args.context is not SceneView view)
				return;
			
			SceneView.duringSceneGui += ShowDeepSelectionMenu;
			try
			{
				var evt = Event.current;

				view.SendEvent(new Event
				{
					commandName = NSelectionMenuCommand,
					type = EventType.ValidateCommand,
					mousePosition = evt.mousePosition
				});

				view.SendEvent(new Event
				{
					commandName = NSelectionMenuCommand,
					type = EventType.ExecuteCommand,
					mousePosition = evt.mousePosition
				});

			}
			finally
			{
				SceneView.duringSceneGui -= ShowDeepSelectionMenu;
			}
		}
		
		private static void ShowDeepSelectionMenu(SceneView view) => OpenDeepSelectionMenu(view, Event.current);
#endif
		
#if !UNITY_2023_3_OR_NEWER
		private static void OnSceneGUI(SceneView sceneView)
		{
			Event e = Event.current;

			if (e.type == EventType.Used || !e.control || !e.isMouse || e.button != 1 || e.shift || e.alt)
				return;

			switch (e.rawType)
			{
				case EventType.MouseDown:
					_mouseRightIsDownWithoutDrag = true;
					break;
				case EventType.MouseDrag:
					_mouseRightIsDownWithoutDrag = false;
					break;
			}

			//The actual CTRL+RIGHT-MOUSE functionality
			if (!_mouseRightIsDownWithoutDrag || e.rawType != EventType.MouseUp)
				return;

			OpenDeepSelectionMenu(sceneView, e);
		}
#endif

		private static void OpenDeepSelectionMenu(SceneView sceneView, Event e)
		{
#if UNITY_2023_3_OR_NEWER
			if (e.type == EventType.ValidateCommand && e.commandName == NSelectionMenuCommand)
				e.Use();

			if(e.type != EventType.ExecuteCommand || e.commandName != NSelectionMenuCommand)
				return;

			e.Use();
#endif
			
			IEnumerable<GameObject> allOverlapping = GetAllOverlapping(e.mousePosition);
			List<SelectionItem> totalSelection = SelectionPopup.TotalSelection;
			totalSelection.Clear();
			foreach (GameObject overlapping in allOverlapping)
			{
				//Check whether the parents of a rect transform have a disabled canvas in them.
				if (overlapping.transform is RectTransform rectTransform)
				{
					var canvas = rectTransform.GetComponentInParent<Canvas>();
					if (canvas != null)
					{
						if (!canvas.enabled)
							continue;
						bool canvasInParentsEnabled = true;
						while (canvas != null)
						{
							Transform parent = canvas.transform.parent;
							if (parent != null)
							{
								canvas = parent.GetComponentInParent<Canvas>();
								if (canvas == null || canvas.enabled)
									continue;
								canvasInParentsEnabled = false;
							}

							break;
						}

						if (!canvasInParentsEnabled)
							continue;
					}
				}

				Component[] components = overlapping.GetComponents<Component>();
				GUIContent[] icons = new GUIContent[components.Length - 1];
				for (var i = 1; i < components.Length; i++)
				{
					if (components[i] == null)
					{
						icons[i - 1] = GUIContent.none;
						continue;
					}

					//Skip the Transform component because it's always the first object
					icons[i - 1] = new GUIContent(AssetPreview.GetMiniThumbnail(components[i]),
						ObjectNames.NicifyVariableName(components[i].GetType().Name));
				}

				totalSelection.Add(new SelectionItem(overlapping, icons));
			}

			if (totalSelection.Count == 0)
				return;

			Vector2 selectionPosition = e.mousePosition;
			float xOffset;
			// Screen-rect limits offset.
			if (selectionPosition.x + SelectionPopup.Width > sceneView.position.width)
				xOffset = -SelectionPopup.Width;
			else
				xOffset = 0;
			int value = Mathf.CeilToInt((selectionPosition.y + SelectionPopup.Height * totalSelection.Count -
				sceneView.position.height + 10) / SelectionPopup.Height);
			SelectionPopup.ScrollPosition = Mathf.Max(0, value);

			// Display popup.
			var buttonRect = new Rect(
				e.mousePosition.x + xOffset - 1,
				e.mousePosition.y - 6,
				0, 0
			);

			e.alt = false;
#if !UNITY_2023_3_OR_NEWER
			_mouseRightIsDownWithoutDrag = false;
			e.Use();
#endif

			PopupWindow.Show(buttonRect, new SelectionPopup());

			// No events after Show. ExitGUI is called.
		}

		#region Overlapping

		private static IEnumerable<GameObject> GetAllOverlapping(Vector2 position)
		{
#if !UNITY_2022_2_OR_NEWER
			return _getAllOverlapping.Invoke(position);
		}

		private static readonly Func<Vector2, IEnumerable<GameObject>> _getAllOverlapping =
			(Func<Vector2, IEnumerable<GameObject>>)Delegate.CreateDelegate(
				typeof(Func<Vector2, IEnumerable<GameObject>>),
				SceneViewPickingClass.GetMethod("GetAllOverlapping", NonPublicStatic)
			);
#else
			args1[0] = position;
			IEnumerable results = (IEnumerable)GetAllOverlappingMethod.Invoke(null, args1);
			foreach (object o in results)
			{
				Object target = (Object)PickingObjectTarget.GetValue(o);
				if(target == null)
					continue;
				switch (target)
				{
					case GameObject gameObject:
						yield return gameObject;
						break;
					case Component component:
						yield return component.gameObject;
						break;
				}
			}
		}

		private static readonly object[] args1 = new object[1];

		private static MethodInfo _getAllOverlappingMethod;
		private static MethodInfo GetAllOverlappingMethod => _getAllOverlappingMethod ??= SceneViewPickingClass.GetMethod("GetAllOverlapping", NonPublicStatic);
		
		private static PropertyInfo _pickingObjectTarget;
		private static PropertyInfo PickingObjectTarget => _pickingObjectTarget ??= Type.GetType("UnityEditor.PickingObject,UnityEditor").GetProperty("target", PublicInstance);
#endif

		private static Type _sceneViewPickingClass;
		private static Type SceneViewPickingClass => _sceneViewPickingClass ??
		                                             (_sceneViewPickingClass =
			                                             Type.GetType("UnityEditor.SceneViewPicking,UnityEditor"));

		#endregion

		#region Hierarchy Window Manipulation

		private static Type _sceneHierarchyType;
		private static Type SceneHierarchyType => _sceneHierarchyType ??
		                                          (_sceneHierarchyType =
			                                          Type.GetType("UnityEditor.SceneHierarchy,UnityEditor"));
		private static Type _sceneHierarchyWindowType;
		private static Type SceneHierarchyWindowType => _sceneHierarchyWindowType ??
		                                                (_sceneHierarchyWindowType =
			                                                Type.GetType(
				                                                "UnityEditor.SceneHierarchyWindow,UnityEditor"));
		private static Type _treeViewController;
		private static Type TreeViewController => _treeViewController ?? (_treeViewController =
			Type.GetType("UnityEditor.IMGUI.Controls.TreeViewController,UnityEditor"));
		private static EditorWindow _hierarchyWindow;
		public static EditorWindow HierarchyWindow =>
			_hierarchyWindow == null ? _hierarchyWindow = GetHierarchyWindow() : _hierarchyWindow;

		public static object SceneHierarchy => SceneHierarchyWindowType
			.GetField("m_SceneHierarchy", NonPublicInstance).GetValue(HierarchyWindow);

		/// <summary>
		/// Sets the hierarchy's expanded state to <see cref="state"/>. <see cref="state"/> must be sorted.
		/// </summary>
		/// <param name="state">A list of ids representing items in the hierarchy.</param>
		/// <param name="sceneHierarchy"><see cref="SceneHierarchy"/></param>
		public static void SetHierarchyToState(List<int> state, object sceneHierarchy = null)
		{
			sceneHierarchy = sceneHierarchy ?? SceneHierarchy;
			TreeViewState treeViewState = (TreeViewState)SceneHierarchyType
				.GetProperty("treeViewState", NonPublicInstance).GetValue(sceneHierarchy);
			treeViewState.expandedIDs = state;
			// Reload the state data because otherwise the tree view does not actually collapse.
			MethodInfo reloadDataMI = TreeViewController.GetMethod("ReloadData");
			reloadDataMI.Invoke(
				SceneHierarchyType.GetProperty("treeView", NonPublicInstance)
					.GetValue(sceneHierarchy), null);
		}
		
		private static void FocusGenericHierarchyWithProperty(object stateParent,
			string treeViewPropertyName,
			BindingFlags flags = NonPublicInstance)
		{
			Type windowType = stateParent.GetType();
			object treeView = windowType
				.GetProperty(treeViewPropertyName, flags)
				.GetValue(stateParent);
			FocusGenericHierarchy(treeView);
		}

		private static void FocusGenericHierarchyWithField(object window,
			string treeViewFieldName,
			BindingFlags flags = NonPublicInstance)
		{
			Type windowType = window.GetType();
			object treeView = windowType
				.GetField(treeViewFieldName, flags)
				.GetValue(window);
			FocusGenericHierarchy(treeView);
		}
		
		private static void FocusGenericHierarchy(object treeView)
		{
			TreeViewState treeViewState = (TreeViewState)TreeViewController
				.GetProperty("state", PublicInstance)
				.GetValue(treeView);
			treeViewState.expandedIDs = new List<int>();

			object data = TreeViewController.GetProperty("data", PublicInstance).GetValue(treeView);
			Type dataSourceType = data.GetType();
			MethodInfo findItem = dataSourceType.GetMethod("FindItem", PublicInstance);
			

			HashSet<int> expandedSet = new HashSet<int>();
			object[] args1 = new object[1];
			foreach (int i in treeViewState.selectedIDs)
			{
				args1[0] = i;
				TreeViewItem item = (TreeViewItem)findItem.Invoke(data, args1);
				if (item == null)
					continue;
				TreeViewItem parent = item.parent;
				while (parent != null)
				{
					expandedSet.Add(parent.id);
					parent = parent.parent;
				}
			}

			args1[0] = expandedSet.ToArray();
			dataSourceType.GetMethod("SetExpandedIDs", PublicInstance).Invoke(data, args1);
		}


		private static EditorWindow GetHierarchyWindow()
		{
			Object[] findObjectsOfTypeAll = Resources.FindObjectsOfTypeAll(SceneHierarchyWindowType);
			if (findObjectsOfTypeAll.Length > 0)
				return (EditorWindow)findObjectsOfTypeAll[0];
			return null;
		}

		/// <summary>
		/// Gets the expanded state of the hierarchy window.
		/// </summary>
		/// <returns>IDs representing expanded hierarchy items.</returns>
		public static int[] GetHierarchyExpandedState()
		{
			if (HierarchyWindow == null)
				return null;
			MethodInfo GetExpandedGameObjectsMI =
				SceneHierarchyWindowType.GetMethod("GetExpandedIDs",
					NonPublicInstance);
			return (int[])GetExpandedGameObjectsMI.Invoke(HierarchyWindow, null);
		}

		internal static void CollectParents(GameObject gameObject, HashSet<GameObject> result)
		{
			Transform t = gameObject.transform;
			while (t.parent != null)
			{
				t = t.parent;
				if (!result.Add(t.gameObject))
					return;
			}
		}

		/// <summary>
		/// Converts an ID returned by <see cref="GetHierarchyExpandedState"/> to an <see cref="Object"/>.
		/// </summary>
		/// <param name="id">An ID associated with the scene view hierarchy.</param>
		/// <param name="sceneHierarchy"><see cref="SceneHierarchy"/> is passed manually to avoid repeated access in tight loops.</param>
		/// <returns>The <see cref="Object"/> associated with the <see cref="id"/></returns>
		public static Object HierarchyIdToObject(int id, object sceneHierarchy = null)
		{
			sceneHierarchy = sceneHierarchy ?? SceneHierarchy;
			object controller =
				SceneHierarchyType.GetProperty("treeView", NonPublicInstance)
					.GetValue(sceneHierarchy);
			MethodInfo findItemMethod =
				controller.GetType().GetMethod("FindItem", PublicInstance);
			object result = findItemMethod.Invoke(controller, new object[] { id });
			if (result == null)
				return null;
			PropertyInfo objectPptrProperty =
				result.GetType().GetProperty("objectPPTR", PublicInstance);
			return (Object)objectPptrProperty.GetValue(result);
		}

		#endregion
	}
}