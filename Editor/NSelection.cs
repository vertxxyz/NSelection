/* Thomas Ingram 2018 */

#if UNITY_2021_1_OR_NEWER
#define USES_PROPERTIES
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable ConvertToNullCoalescingCompoundAssignment

namespace Vertx
{
	[InitializeOnLoad]
	public class NSelection
	{
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
			_mouseRightIsDownWithoutDrag = false;
			e.Use();

			PopupWindow.Show(buttonRect, new SelectionPopup());

			// No events after Show. ExitGUI is called.
		}

		#region Overlapping

		private static IEnumerable<GameObject> GetAllOverlapping(Vector2 position) =>
			_getAllOverlapping.Invoke(position);

		private static readonly Func<Vector2, IEnumerable<GameObject>> _getAllOverlapping =
			(Func<Vector2, IEnumerable<GameObject>>)Delegate.CreateDelegate(
				typeof(Func<Vector2, IEnumerable<GameObject>>),
				SceneViewPickingClass.GetMethod("GetAllOverlapping", BindingFlags.Static | BindingFlags.NonPublic)
			);

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
			.GetField("m_SceneHierarchy", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(HierarchyWindow);

		/// <summary>
		/// Sets the hierarchy's expanded state to <see cref="state"/>. <see cref="state"/> must be sorted.
		/// </summary>
		/// <param name="state">A list of ids representing items in the hierarchy.</param>
		/// <param name="sceneHierarchy"><see cref="SceneHierarchy"/></param>
		public static void SetHierarchyToState(List<int> state, object sceneHierarchy = null)
		{
			sceneHierarchy = sceneHierarchy ?? SceneHierarchy;
			TreeViewState treeViewState = (TreeViewState)SceneHierarchyType
				.GetProperty("treeViewState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sceneHierarchy);
			treeViewState.expandedIDs = state;
			// Reload the state data because otherwise the tree view does not actually collapse.
			MethodInfo reloadDataMI = TreeViewController.GetMethod("ReloadData");
			reloadDataMI.Invoke(
				SceneHierarchyType.GetProperty("treeView", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(sceneHierarchy), null);
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
					BindingFlags.NonPublic | BindingFlags.Instance);
			return (int[])GetExpandedGameObjectsMI.Invoke(HierarchyWindow, null);
		}

		internal static void CollectHierarchyGameObjects(GameObject gameObject, HashSet<GameObject> result)
		{
			Transform t = gameObject.transform;
			result.Add(gameObject);
			while (t.parent != null)
			{
				t = t.parent;
				if (!result.Add(t.gameObject))
					return;
			}
		}

		/// <summary>
		/// Collapses everything in the hierarchy window except the current selection and any uncollapsed scenes.
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
				CollectHierarchyGameObjects(selected, selection);

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
		/// Collapses everything in the hierarchy window.
		/// </summary>
		[Shortcut("Hierarchy View/Collapse Hierarchy Completely")]
		public static void CollapseHierarchyCompletely()
		{
			if (HierarchyWindow == null)
				return;

			SetHierarchyToState(new List<int>());
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
				SceneHierarchyType.GetProperty("treeView", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(sceneHierarchy);
			MethodInfo findItemMethod =
				controller.GetType().GetMethod("FindItem", BindingFlags.Public | BindingFlags.Instance);
			object result = findItemMethod.Invoke(controller, new object[] { id });
			if (result == null)
				return null;
			PropertyInfo objectPptrProperty =
				result.GetType().GetProperty("objectPPTR", BindingFlags.Public | BindingFlags.Instance);
			return (Object)objectPptrProperty.GetValue(result);
		}
		#endregion
	}

	internal class SelectionItem
	{
		public GameObject GameObject { get; }
		public GUIContent[] Icons { get; }

		public SelectionItem(GameObject gameObject, GUIContent[] icons)
		{
			GameObject = gameObject;
			Icons = icons;
		}
	}

	internal class SelectionPopup : PopupWindowContent
	{
		public static List<SelectionItem> TotalSelection = new List<SelectionItem>();
		private float _iconOffset;
		private float _iconOffsetTarget;
		private static int _currentlyHoveringIndex;

		public static float ScrollPosition;
		private Vector2 _originalPosition;
		private bool _initialised;
		private const int MaxIcons = 7;

		#region Styling

		private static Color BoxBorderColor => new Color(0, 0, 0, 1);

		// Mini white label is used for highlighted content.
		private static GUIStyle _miniLabelWhite;

		private static GUIStyle MiniLabelWhite =>
			_miniLabelWhite ?? (_miniLabelWhite = new GUIStyle(EditorStyles.miniLabel)
			{
				normal = { textColor = Color.white },
				onNormal = { textColor = Color.white },
				hover = { textColor = Color.white },
				onHover = { textColor = Color.white },
				active = { textColor = Color.white },
				onActive = { textColor = Color.white },
			});

		// We can't just use EditorStyles.miniLabel because it's not black in the pro-skin.
		private static GUIStyle _miniLabelBlack;

		private static GUIStyle MiniLabelBlack =>
			_miniLabelBlack ?? (_miniLabelBlack = new GUIStyle(EditorStyles.miniLabel)
			{
				normal = { textColor = Color.black },
				onNormal = { textColor = Color.black },
				hover = { textColor = Color.black },
				onHover = { textColor = Color.black },
				active = { textColor = Color.black },
				onActive = { textColor = Color.black },
			});

		#endregion

		public SelectionPopup() => RefreshSelection();

		public override void OnOpen()
		{
			base.OnOpen();
#if UNITY_2019_1_OR_NEWER
			SceneView.duringSceneGui += CaptureEvents;
#else
			SceneView.onSceneGUIDelegate += CaptureEvents;
#endif
			_originalPosition = editorWindow.position.position;
			editorWindow.wantsMouseMove = true;
		}

		public const float Width = 250f;
		public const float Height = 16f;

		public override Vector2 GetWindowSize() => new Vector2(Width, Height * TotalSelection.Count + 1 + 5 * 2);

		private void CaptureEvents(SceneView sceneView)
		{
			GUIUtility.hotControl = 0;
			Event e = Event.current;
			switch (e.type)
			{
				case EventType.Repaint:
				case EventType.Layout:
					return;
				case EventType.ScrollWheel:
					_scrollDelta = Math.Sign(e.delta.y);
					break;
			}

			e.Use();
		}

		public override void OnClose()
		{
			base.OnClose();
			// Locks the scene view from receiving input for one more frame - which is enough to stop clicking off the UI from selecting a new object.
			EditorApplication.delayCall += () =>
			{
#if UNITY_2019_1_OR_NEWER
				SceneView.duringSceneGui -= CaptureEvents;
#else
				SceneView.onSceneGUIDelegate -= CaptureEvents;
#endif
			};
		}

		private int _scrollDelta;
		private static readonly HashSet<GameObject> _currentSelection = new HashSet<GameObject>();
		private int _lastHighlightedIndex = -1;
		private bool _additionWasLastHeldForPreview;

		public override void OnGUI(Rect position)
		{
			if (!_initialised)
			{
				_originalPosition = editorWindow.position.position;
				_initialised = true;
			}

			Event e = Event.current;
			bool additive = e.control || e.command || e.shift;
			
			GUIUtility.hotControl = 0;

			int highlightedIndex = -1;

			// Scrolling behaviour.
			if (e.type == EventType.ScrollWheel)
				_scrollDelta = Math.Sign(e.delta.y);

			if (_scrollDelta != 0)
			{
				ScrollPosition += _scrollDelta;
				ScrollPosition = Mathf.Clamp(ScrollPosition, 0, TotalSelection.Count - 1);
				Rect tempRect = editorWindow.position;
				tempRect.position = new Vector2(_originalPosition.x, _originalPosition.y - ScrollPosition * Height);
				editorWindow.position = tempRect;
				_scrollDelta = 0;
			}

			//Top and bottom borders (to fix a weird issue where the top 5 pixels of the window do not receive mouse events)
			EditorGUI.DrawRect(new Rect(0, 0, Width, 5), GUI.color);
			EditorGUI.DrawRect(new Rect(0, 6 + Height * TotalSelection.Count, Width, 5), GUI.color);

			Rect separatorTopRect = new Rect(0, 5, Width, 1);
			EditorGUI.DrawRect(separatorTopRect, BoxBorderColor);

			for (var i = 0; i < TotalSelection.Count; i++)
			{
				SelectionItem selectionItem = TotalSelection[i];
				GameObject gameObject = selectionItem.GameObject;
				GUIContent[] icons = selectionItem.Icons;

				Rect boxRect = new Rect(0, 5 + 1 + i * Height, Width, Height);
				GUIStyle labelStyle;

				bool isInSelection = _currentSelection.Contains(gameObject);
				bool containsMouse = boxRect.Contains(e.mousePosition);
				if (containsMouse)
					highlightedIndex = i;

				EditorGUI.DrawRect(boxRect, BoxBorderColor);
				
				if (isInSelection)
				{
					if (containsMouse)
					{
						// If we're not holding it will solely select this object, otherwise it will be a deselection.
						GUI.color = !additive
							? new Color(0f, 0.5f, 1f)
							: new Color(0.58f, 0.62f, 0.75f);
					}
					else
					{
						// If we're not holding control and we're not hovering it will deselect these, so show that preview.
						// Otherwise, we will be selecting additionally.
						GUI.color = !additive
							? new Color(0.58f, 0.62f, 0.75f)
							: new Color(0f, 0.5f, 1f);
					}

					labelStyle = MiniLabelWhite;
				}
				else
				{
					if (containsMouse) // Going to select.
					{
						GUI.color = new Color(0f, 0.5f, 1f);
						labelStyle = MiniLabelWhite;
					}
					else // Not in selection.
					{
						GUI.color = Color.white;
						labelStyle = MiniLabelBlack;
					}
				}

				Rect innerBoxRect = new Rect(boxRect.x + 1, boxRect.y, boxRect.width - 2, boxRect.height - 1);
				EditorGUI.DrawRect(innerBoxRect, GUI.color);
				GUI.color = Color.white;
				GUI.Label(new Rect(boxRect.x + 20, boxRect.y, Width - 20, boxRect.height), gameObject.name, labelStyle);

				int maxLength = Mathf.Min(icons.Length, MaxIcons);
				float width = Height * maxLength;

				if (icons.Length > 0)
				{
					Rect iconsRect = new Rect(boxRect.x + boxRect.width - width, boxRect.y, width,
						Height);

					// Behaviour for scrolling icons when the cursor is over the selection (only if the icon count is greater than MaxIcons)
					if (containsMouse && maxLength < icons.Length)
					{
						if (_currentlyHoveringIndex != i)
						{
							_currentlyHoveringIndex = i;
							_iconOffset = 0;
						}

						float max = icons.Length - maxLength;
						_iconOffset = Mathf.MoveTowards(_iconOffset, _iconOffsetTarget,
							(float)(EditorApplication.timeSinceStartup - lastTime));
						if (_iconOffset <= 0)
						{
							_iconOffset = 0;
							_iconOffsetTarget = max;
						}
						else if (_iconOffset >= max)
						{
							_iconOffset = max;
							_iconOffsetTarget = 0;
						}
					}

					using (new GUI.GroupScope(iconsRect))
					{
						for (var j = 0; j < icons.Length; j++)
						{
							GUIContent icon = icons[j];
							GUI.Label(
								new Rect(width - (maxLength - j) * Height - _iconOffset * Height, 0, Height, Height),
								icon);
						}
					}
				}

				// If the selection is being hovered we may need to modify the currently previewing selection
				// or if the control key has changes states.
				if (containsMouse && (i != _lastHighlightedIndex || _additionWasLastHeldForPreview != additive))
				{
					_lastHighlightedIndex = i;
					if (!additive)
					{
						_additionWasLastHeldForPreview = false;
						ResetHierarchyToExpandedStateExcept(gameObject);
						// If we're not selecting more (ie. have control held) we should just set the selection to be the hovered item.
						Selection.objects = Array.Empty<Object>();
						Selection.activeGameObject = gameObject;
					}
					else
					{
						_additionWasLastHeldForPreview = true;
						// Otherwise we need to alter the current selection to add or remove the currently hovered selection.
						if (isInSelection)
						{
							ResetHierarchyToExpandedState();
							// Remove the GameObject.
							Object[] newSelection = new Object[_currentSelection.Count - 1];
							int n = 0;
							foreach (GameObject o in _currentSelection)
							{
								if (o == gameObject) continue;
								newSelection[n++] = o;
							}

							Selection.objects = newSelection;
						}
						else
						{
							ResetHierarchyToExpandedStateExcept(gameObject);
							// Add the GameObject.
							Object[] newSelection = new Object[_currentSelection.Count + 1];
							int n = 0;
							foreach (GameObject o in _currentSelection)
								newSelection[n++] = o;
							newSelection[n] = gameObject;
							Selection.objects = newSelection;
						}
					}
				}

				// Clicked in the box!
				if (containsMouse && e.isMouse && e.type == EventType.MouseUp)
				{
					MakeSelection(i, additive);
					e.Use();
					if (!additive)
						break;
				}
			}

			if (highlightedIndex == -1 && _lastHighlightedIndex != -1)
				RevertPreviewSelection();

			if (e.isKey && e.type == EventType.KeyUp)
			{
				switch (e.keyCode)
				{
					case KeyCode.Escape:
						RevertPreviewSelection();
						EndSelection();
						break;
					case KeyCode.Return:
						if (highlightedIndex >= 0)
							MakeSelection(highlightedIndex, additive);
						break;
				}
			}
			else if (e.isMouse && e.type == EventType.MouseUp)
			{
				if (highlightedIndex == -1)
					RevertPreviewSelection();
				EndSelection();
			}

			if (e.type != EventType.Repaint && e.type != EventType.Layout)
				e.Use();

			lastTime = EditorApplication.timeSinceStartup;
		}

		private double lastTime;

		private void EndSelection()
		{
			// Fix issues where the FPS controls are stuck on.
			Tools.viewTool = ViewTool.None;

			ScrollPosition = 0;
			TotalSelection.Clear();
			_currentSelection.Clear();
			SceneView.RepaintAll();
			editorWindow.Close();
		}

		/// <summary>
		/// Reverts the currently active selection preview. The selection is visualised before selection, and this method removes the visualisation.
		/// </summary>
		private void RevertPreviewSelection()
		{
			ResetHierarchyToExpandedState();
			_lastHighlightedIndex = -1;
			//Revert to _currentSelection
			Object[] newSelection = new Object[_currentSelection.Count];
			int n = 0;
			foreach (GameObject g in _currentSelection)
				newSelection[n++] = g;
			Selection.objects = newSelection;
		}

		private static void RefreshSelection()
		{
			SetHierarchyExpandedState();
			_currentSelection.Clear();
			_currentSelection.UnionWith(Selection.gameObjects);
		}

		private void MakeSelection(int index, bool isShift)
		{
			GameObject gameObject = TotalSelection[index].GameObject;
			bool selectionContains = _currentSelection.Contains(gameObject);

			if (isShift)
			{
				HashSet<Object> objects = new HashSet<Object>(Selection.objects);
				if (!selectionContains)
					objects.Add(gameObject);
				else
					objects.Remove(gameObject);

				Selection.objects = objects.ToArray();
			}
			else
			{
				Selection.objects = Array.Empty<Object>();
				Selection.activeGameObject = gameObject;
			}

			if (!isShift)
				EndSelection();
			else
				RefreshSelection();
			SceneView.RepaintAll();
		}

		private static int[] _allExpandedIDs;

		private static void SetHierarchyExpandedState() => _allExpandedIDs = NSelection.GetHierarchyExpandedState();

		private static void ResetHierarchyToExpandedState()
		{
			if (_allExpandedIDs == null)
				return;

			if (NSelection.HierarchyWindow == null)
				return;

			NSelection.SetHierarchyToState(new List<int>(_allExpandedIDs));
		}

		private static void ResetHierarchyToExpandedStateExcept(GameObject gameObject)
		{
			if (_allExpandedIDs == null)
				return;

			if (NSelection.HierarchyWindow == null)
				return;

			var newState = new List<int>(_allExpandedIDs);

			HashSet<GameObject> selection = new HashSet<GameObject>();
			NSelection.CollectHierarchyGameObjects(gameObject, selection);

			object sceneHierarchy = NSelection.SceneHierarchy;
			int[] expandedState = NSelection.GetHierarchyExpandedState();
			// Persist the selection in the state.
			foreach (int i in expandedState)
			{
				Object o = NSelection.HierarchyIdToObject(i, sceneHierarchy);
				if (!selection.Contains(o))
					continue;
				if (!newState.Contains(i))
					newState.Add(i);
			}

			// If unsorted, the hierarchy will break.
			newState.Sort();

			NSelection.SetHierarchyToState(newState);
		}
	}
}