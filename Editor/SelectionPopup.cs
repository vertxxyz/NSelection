using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
// ReSharper disable ConvertToNullCoalescingCompoundAssignment

namespace Vertx
{
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
			NSelection.CollectParents(gameObject, selection);

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