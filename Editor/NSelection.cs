/*Thomas Ingram 2018*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vertx
{
	[InitializeOnLoad]
	public class NSelection
	{
		static NSelection() => RefreshListeners();

		static void RefreshListeners()
		{
			#if UNITY_2019_1_OR_NEWER
			SceneView.duringSceneGui -= OnSceneGUI;
			SceneView.duringSceneGui += OnSceneGUI;
			#else
			SceneView.onSceneGUIDelegate -= OnSceneGUI;
			SceneView.onSceneGUIDelegate += OnSceneGUI;
			#endif
		}

		static bool mouseRightIsDownWithoutDrag;

		public const float width = 250f;
		public const float height = 16f;

		static void OnSceneGUI(SceneView sceneView)
		{
			Event e = Event.current;

			if (e.type != EventType.Used && e.control && e.isMouse && e.button == 1)
			{
				switch (e.rawType)
				{
					case EventType.MouseDown:
						mouseRightIsDownWithoutDrag = true;
						break;
					case EventType.MouseDrag:
						mouseRightIsDownWithoutDrag = false;
						break;
				}

				//The actual CTRL+RIGHT-MOUSE functionality
				if (mouseRightIsDownWithoutDrag && e.rawType == EventType.MouseUp)
				{
					SelectionPopup.totalSelection = GetAllOverlapping(e.mousePosition).ToArray();

					SelectionPopup.icons = new GUIContent[SelectionPopup.totalSelection.Length][];
					SelectionPopup.iconsOffsets = new float[SelectionPopup.totalSelection.Length];
					SelectionPopup.iconsOffsetTargets = new float[SelectionPopup.totalSelection.Length];
					for (var t = 0; t < SelectionPopup.totalSelection.Length; t++)
					{
						GameObject gO = SelectionPopup.totalSelection[t];
						Component[] cS = gO.GetComponents<Component>();
						GUIContent[] icons = new GUIContent[cS.Length - 1];
						for (var i = 1; i < cS.Length; i++)
						{
							//Skip the Transform component because it's always the first object
							icons[i - 1] = new GUIContent(AssetPreview.GetMiniThumbnail(cS[i]), ObjectNames.NicifyVariableName(cS[i].GetType().Name));
						}

						SelectionPopup.icons[t] = icons;
						SelectionPopup.iconsOffsets[t] = 0;
						SelectionPopup.iconsOffsetTargets[t] = 0;
					}

					Vector2 selectionPosition = e.mousePosition;
					float xOffset;
					//Screen-rect limits offset
					if (selectionPosition.x + width > sceneView.position.width)
						xOffset = -width;
					else
						xOffset = 0;
					int value = Mathf.CeilToInt(((selectionPosition.y + height * SelectionPopup.totalSelection.Length) - sceneView.position.height + 10) / height);
					SelectionPopup.scrollPosition = Mathf.Max(0, value);

					SelectionPopup popup = SelectionPopup.ShowModal(
						new Rect(
							sceneView.position.x + e.mousePosition.x + xOffset,
							sceneView.position.y + e.mousePosition.y + height * 1.5f,
							width,
							height * SelectionPopup.totalSelection.Length + 1 + 5*2
						)
					);
					if (popup == null)
					{
						e.Use();
						return;
					}


					e.alt = false;
					mouseRightIsDownWithoutDrag = false;
					SelectionPopup.RefreshSelection();

					e.Use();
				}
			}
		}

		#region Overlapping

		static IEnumerable<GameObject> GetAllOverlapping(Vector2 position) => (IEnumerable<GameObject>) getAllOverlapping.Invoke(null, new object[] {position});

		static MethodInfo _getAllOverlapping;

		static MethodInfo getAllOverlapping => _getAllOverlapping ?? (_getAllOverlapping = sceneViewPickingClass.GetMethod("GetAllOverlapping", BindingFlags.Static | BindingFlags.NonPublic));

		static Type _sceneViewPickingClass;

		static Type sceneViewPickingClass => _sceneViewPickingClass ?? (_sceneViewPickingClass = Type.GetType("UnityEditor.SceneViewPicking,UnityEditor"));

		#endregion
	}

	public class SelectionPopup : EditorWindow
	{
		public static GUIContent[][] icons;
		public static float[] iconsOffsets;
		public static float[] iconsOffsetTargets;

		public static float scrollPosition;
		private static Vector2 originalPosition;
		private const int maxIcons = 7;

		#region Styling

		private static Color boxBorderColor => new Color(0, 0, 0, 1);

		//Mini white label is used for highlighted content
		private static GUIStyle _miniLabelWhite;

		private static GUIStyle miniLabelWhite =>
			_miniLabelWhite ?? (_miniLabelWhite = new GUIStyle(EditorStyles.miniLabel)
			{
				normal = {textColor = Color.white},
				onNormal = {textColor = Color.white},
				hover = {textColor = Color.white},
				onHover = {textColor = Color.white},
				active = {textColor = Color.white},
				onActive = {textColor = Color.white},
			});

		//We can't just use EditorStyles.miniLabel because it's not black in the pro-skin
		private static GUIStyle _miniLabelBlack;

		private static GUIStyle miniLabelBlack =>
			_miniLabelBlack ?? (_miniLabelBlack = new GUIStyle(EditorStyles.miniLabel)
			{
				normal = {textColor = Color.black},
				onNormal = {textColor = Color.black},
				hover = {textColor = Color.black},
				onHover = {textColor = Color.black},
				active = {textColor = Color.black},
				onActive = {textColor = Color.black},
			});

		#endregion

		public static SelectionPopup ShowModal(Rect r)
		{
			if (totalSelection == null || totalSelection.Length == 0)
				return null;
			SelectionPopup popup = CreateInstance<SelectionPopup>();
			popup.ShowAsDropDown(new Rect(r.position, Vector2.zero), r.size);
			originalPosition = r.position;
			return popup;
		}

		private void OnEnable()
		{
			#if UNITY_2019_1_OR_NEWER
			SceneView.duringSceneGui += CaptureEvents;
			#else
			SceneView.onSceneGUIDelegate += CaptureEvents;
			#endif
		}

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
					scrollDelta = Math.Sign(e.delta.y);
					break;
			}

			e.Use();
		}

		private void OnDisable()
		{
			//Locks the scene view from receiving input for one more frame - which is enough to stop clicking off the UI from selecting a new object
			EditorApplication.delayCall += () =>
			{
				#if UNITY_2019_1_OR_NEWER
				SceneView.duringSceneGui -= CaptureEvents;
				#else
				SceneView.onSceneGUIDelegate -= CaptureEvents;
				#endif
			};
		}

		private int scrollDelta;

		public static GameObject[] totalSelection;

		private static readonly HashSet<GameObject> currentSelection = new HashSet<GameObject>();
		private int lastIndexHighlighted = -1;
		private bool shiftWasHeldForPreview;

		void OnGUI()
		{
			Event e = Event.current;
			GUIUtility.hotControl = 0;

			int indexCurrentlyHighlighted = -1;

			//Scrolling behaviour
			if (e.type == EventType.ScrollWheel)
				scrollDelta = Math.Sign(e.delta.y);

			if (scrollDelta != 0)
			{
				scrollPosition += scrollDelta;
				scrollPosition = Mathf.Clamp(scrollPosition, 0, totalSelection.Length - 1);
				Rect tempRect = position;
				tempRect.position = new Vector2(originalPosition.x, originalPosition.y - scrollPosition * NSelection.height);
				ShowAsDropDown(new Rect(tempRect.position, Vector2.zero), tempRect.size);
				scrollDelta = 0;
			}

			
			//Top and bottom borders (to fix a weird issue where the top 5 pixels of the window do not receive mouse events)
			EditorGUI.DrawRect(new Rect(0, 0, NSelection.width, 5), GUI.color);
			EditorGUI.DrawRect(new Rect(0, 6 + NSelection.height * totalSelection.Length, NSelection.width, 5), GUI.color);
			
			
			Rect separatorTopRect = new Rect(0, 5, NSelection.width, 1);
			EditorGUI.DrawRect(separatorTopRect, boxBorderColor);

			for (var i = 0; i < totalSelection.Length; i++)
			{
				GameObject gameObject = totalSelection[i];

				Rect boxRect = new Rect(0, 5 + 1 + i * NSelection.height, NSelection.width, NSelection.height);
				GUIStyle labelStyle;

				bool isInSelection = currentSelection.Contains(gameObject);
				bool contains = boxRect.Contains(e.mousePosition);
				if (contains)
					indexCurrentlyHighlighted = i;

				EditorGUI.DrawRect(boxRect, boxBorderColor);

				if (isInSelection)
				{
					if (contains)
					{
						//If we're not holding shift it will solely select this object
						if (!e.shift)
							GUI.color = new Color(0f, 0.5f, 1f);
						else //Otherwise it will be a deselection
							GUI.color = new Color(0.58f, 0.62f, 0.75f);
					}
					else
					{
						//If we're not holding shift and we're not hovering it will deselect these, so show that preview.
						if (!e.shift)
							GUI.color = new Color(0.58f, 0.62f, 0.75f);
						else // Otherwise, we will be selecting additionally
							GUI.color = new Color(0f, 0.5f, 1f);
					}

					labelStyle = miniLabelWhite;
				}
				else
				{
					if (contains) //Going To Select
					{
						GUI.color = new Color(0f, 0.5f, 1f);
						labelStyle = miniLabelWhite;
					}
					else //Not In Selection
					{
						GUI.color = Color.white;
						labelStyle = miniLabelBlack;
					}
				}

				Rect innerBoxRect = new Rect(boxRect.x + 1, boxRect.y, boxRect.width - 2, boxRect.height - 1);
				EditorGUI.DrawRect(innerBoxRect, GUI.color);
				GUI.color = Color.white;
				GUI.Label(new Rect(boxRect.x + 20, boxRect.y, NSelection.width - 20, boxRect.height), gameObject.name, labelStyle);

				GUIContent[] iconsLocal = icons[i];
				if (iconsLocal.Length > 0)
				{
					int maxLength = Mathf.Min(iconsLocal.Length, maxIcons);
					float width = NSelection.height * maxLength;
					Rect iconsRect = new Rect(boxRect.x + boxRect.width - maxLength * NSelection.height, boxRect.y, width, NSelection.height);
					float iconOffset = 0;

					//Behaviour for scrolling icons when the cursor is over the selection (only if the icon count is greater than maxIcons)
					if (contains && maxLength < iconsLocal.Length)
					{
						float max = iconsLocal.Length - maxLength;
						iconsOffsets[i] = Mathf.MoveTowards(iconsOffsets[i], iconsOffsetTargets[i], (float) (EditorApplication.timeSinceStartup - lastTime));
						if (iconsOffsets[i] <= 0)
						{
							iconsOffsets[i] = 0;
							iconsOffsetTargets[i] = max;
						}
						else if (iconsOffsets[i] >= max)
						{
							iconsOffsets[i] = max;
							iconsOffsetTargets[i] = 0;
						}

						iconOffset = iconsOffsets[i];
					}
					else
						iconsOffsets[i] = iconOffset;

					using (new GUI.GroupScope(iconsRect))
					{
						for (var j = 0; j < iconsLocal.Length; j++)
						{
							GUIContent icon = iconsLocal[j];
							GUI.Label(new Rect(width - (maxLength - j) * NSelection.height - iconOffset * NSelection.height, 0, NSelection.height, NSelection.height), icon);
						}
					}
				}

				//If the selection is being hovered we may need to modify the currently previewing selection
				//or if the shift key has changes states.
				if (contains && (i != lastIndexHighlighted || shiftWasHeldForPreview != e.shift))
				{
					ResetHierarchyToExpandedState();
					lastIndexHighlighted = i;
					if (!e.shift)
					{
						shiftWasHeldForPreview = false;
						//If we're not selecting more (ie. have shift held) we should just set the selection to be the hovered item
						Selection.objects = new Object[0];
						Selection.activeGameObject = gameObject;
					}
					else
					{
						shiftWasHeldForPreview = true;
						//Otherwise we need to alter the current selection to add or remove the currently hovered selection
						if (isInSelection)
						{
							//Remove the GameObject
							Object[] newSelection = new Object[currentSelection.Count - 1];
							int n = 0;
							foreach (GameObject o in currentSelection)
							{
								if (o == gameObject) continue;
								newSelection[n++] = o;
							}

							Selection.objects = newSelection;
						}
						else
						{
							//Add the GameObject
							Object[] newSelection = new Object[currentSelection.Count + 1];
							int n = 0;
							foreach (GameObject o in currentSelection)
								newSelection[n++] = o;
							newSelection[n] = gameObject;
							Selection.objects = newSelection;
						}
					}
				}

				//Clicked in the box!
				if (contains && e.isMouse && e.type == EventType.MouseUp)
				{
					MakeSelection(i, e.shift);
					e.Use();
					if (!e.shift)
						break;
				}
			}

			if (indexCurrentlyHighlighted == -1 && lastIndexHighlighted != -1)
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
						if (indexCurrentlyHighlighted >= 0)
							MakeSelection(indexCurrentlyHighlighted, e.shift);
						break;
				}
			}
			else if (e.isMouse && e.type == EventType.MouseUp)
			{
				if (indexCurrentlyHighlighted == -1)
					RevertPreviewSelection();
				EndSelection();
			}

			if (e.type != EventType.Repaint && e.type != EventType.Layout)
				e.Use();

			Focus();

			Repaint();

			lastTime = EditorApplication.timeSinceStartup;
		}

		private double lastTime;

		void EndSelection()
		{
			//Fix issues where the FPS controls are stuck on
			Tools.viewTool = ViewTool.None;

			scrollPosition = 0;
			totalSelection = null;
			currentSelection.Clear();
			SceneView.RepaintAll();
			Close();
		}

		/// <summary>
		/// Reverts the currently active selection preview. The selection is visualised before selection, and this method removes the visualisation.
		/// </summary>
		void RevertPreviewSelection()
		{
			ResetHierarchyToExpandedState();
			lastIndexHighlighted = -1;
			//Revert to currentSelection
			Object[] newSelection = new Object[currentSelection.Count];
			int n = 0;
			foreach (GameObject g in currentSelection)
				newSelection[n++] = g;
			Selection.objects = newSelection;
		}

		public static void RefreshSelection()
		{
			SetHierarchyExpandedState();
			currentSelection.Clear();
			currentSelection.UnionWith(Selection.gameObjects);
		}

		void MakeSelection(int index, bool isShift)
		{
			GameObject gameObject = totalSelection[index];
			bool selectionContains = currentSelection.Contains(gameObject);

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
				Selection.objects = new Object[0];
				Selection.activeGameObject = gameObject;
			}

			if (!isShift)
				EndSelection();
			else
				RefreshSelection();
			SceneView.RepaintAll();
		}

		#region Hierarchy Window Manipulation

		private static Type _sceneHierarchyType;
		private static Type sceneHierarchyType => _sceneHierarchyType ?? (_sceneHierarchyType = Type.GetType("UnityEditor.SceneHierarchy,UnityEditor"));
		private static Type _sceneHierarchyWindowType;
		private static Type sceneHierarchyWindowType => _sceneHierarchyWindowType ?? (_sceneHierarchyWindowType = Type.GetType("UnityEditor.SceneHierarchyWindow,UnityEditor"));
		private static Type _treeViewController;
		private static Type treeViewController => _treeViewController ?? (_treeViewController = Type.GetType("UnityEditor.IMGUI.Controls.TreeViewController,UnityEditor"));
		private static EditorWindow _hierarchyWindow;
		private static EditorWindow hierarchyWindow => _hierarchyWindow == null ? _hierarchyWindow = GetHierarchyWindow() : _hierarchyWindow;

		private static int[] allExpandedIDs;

		private static void SetHierarchyExpandedState()
		{
			allExpandedIDs = GetAllVisible();
			
			int[] GetAllVisible()
			{
				if (hierarchyWindow == null)
					return null;
				MethodInfo GetExpandedGameObjectsMI = sceneHierarchyWindowType.GetMethod("GetExpandedIDs", BindingFlags.NonPublic | BindingFlags.Instance);
				return (int[]) GetExpandedGameObjectsMI.Invoke(hierarchyWindow, null);
			}
		}

		private static void ResetHierarchyToExpandedState()
		{
			if (allExpandedIDs == null)
				return;
			if (hierarchyWindow == null)
				return;
			object sceneHierarchy = sceneHierarchyWindowType.GetField("m_SceneHierarchy", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hierarchyWindow);
			TreeViewState treeViewState = (TreeViewState) sceneHierarchyType.GetProperty("treeViewState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sceneHierarchy);
			treeViewState.expandedIDs = new List<int>(allExpandedIDs);
			//Reload the state data because otherwise the tree view does not actually collapse.
			MethodInfo reloadDataMI = treeViewController.GetMethod("ReloadData");
			reloadDataMI.Invoke(sceneHierarchyType.GetProperty("treeView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sceneHierarchy), null);
		}

		private static EditorWindow GetHierarchyWindow()
		{
			Object[] findObjectsOfTypeAll = Resources.FindObjectsOfTypeAll(sceneHierarchyWindowType);
			if (findObjectsOfTypeAll.Length > 0)
				return (EditorWindow) findObjectsOfTypeAll[0];
			return null;
		}
		#endregion
	}
}