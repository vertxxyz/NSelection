using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vertx
{
	[InitializeOnLoad]
	public class NSelection
	{
		#region Overlapping

		static IEnumerable<GameObject> GetAllOverlapping(Vector2 position)
		{
			return (IEnumerable<GameObject>) getAllOverlapping.Invoke(null, new object[] {position});
		}

		[SerializeField] static MethodInfo _getAllOverlapping;

		static MethodInfo getAllOverlapping
		{
			get { return _getAllOverlapping ?? (_getAllOverlapping = sceneViewPickingClass.GetMethod("GetAllOverlapping", BindingFlags.Static | BindingFlags.NonPublic)); }
		}

		[SerializeField] static Type _sceneViewPickingClass;

		static Type sceneViewPickingClass
		{
			get { return _sceneViewPickingClass ?? (_sceneViewPickingClass = Type.GetType("UnityEditor.SceneViewPicking,UnityEditor")); }
		}

		#endregion

		#region CustomMenu

		static void DisplayCustomMenu(Rect r, string[] menuNames, int[] selected, EditorUtility.SelectMenuItemFunction callback, object userData)
		{
			displayCustomMenu.Invoke(null, new[] {r, menuNames, selected, callback, userData});
		}

		[SerializeField] static MethodInfo _displayCustomMenu;

		static MethodInfo displayCustomMenu
		{
			get
			{
				return _displayCustomMenu ??
				       (_displayCustomMenu = typeof(EditorUtility).GetMethod("DisplayCustomMenu", BindingFlags.Static | BindingFlags.NonPublic, null,
					       new[] {typeof(Rect), typeof(string[]), typeof(int[]), typeof(EditorUtility.SelectMenuItemFunction), typeof(object)}, null));
			}
		}

		#endregion

		static NSelection()
		{
			RefreshListeners();
		}

		static void RefreshListeners()
		{
			SceneView.onSceneGUIDelegate -= OnSceneGUI;
			SceneView.onSceneGUIDelegate += OnSceneGUI;
		}

		static bool mouseRightIsDownWithoutDrag;

		private static GameObject[] totalSelection;

		private static readonly HashSet<GameObject> currentSelectionHash = new HashSet<GameObject>();
		private static bool isShiftSelection;
		private static Vector2 selectionPosition;

		private static GUIStyle _miniLabelWhite;

		private static GUIStyle miniLabelWhite
		{
			get
			{
				return _miniLabelWhite ?? (_miniLabelWhite = new GUIStyle(EditorStyles.miniLabel)
				{
					normal = {textColor = Color.white},
					onNormal = {textColor = Color.white},
					hover = {textColor = Color.white},
					onHover = {textColor = Color.white},
					active = {textColor = Color.white},
					onActive = {textColor = Color.white},
				});
			}
		}

		private bool useUntilRepaint;
		static void OnSceneGUI(SceneView sceneView)
		{
			Event e = Event.current;

			Handles.BeginGUI();
			{
				if (totalSelection != null && totalSelection.Length > 0)
				{
					EditorGUIUtility.AddCursorRect(new Rect(0f, 17f, Screen.width, Screen.height - 17f), isShiftSelection || e.shift ? MouseCursor.ArrowPlus : MouseCursor.Arrow);
					float h = EditorGUIUtility.singleLineHeight;
					int indexCurrentlyHighlighted = -1;
					for (var i = 0; i < totalSelection.Length; i++)
					{
						GameObject gameObject = totalSelection[i];
						Rect boxRect = new Rect(selectionPosition.x, selectionPosition.y + i * h - h / 2f, 200, h);
						GUIStyle labelStyle;

						bool isInSelection = currentSelectionHash.Contains(gameObject);
						bool contains = boxRect.Contains(e.mousePosition);
						if (contains)
							indexCurrentlyHighlighted = i;

						if (isInSelection)
						{
							if(contains)	//Going to Deselect
								GUI.color = new Color(0.58f, 0.62f, 0.75f);
							else			//Is In Selection
								GUI.color = new Color(0f, 0.5f, 1f);
							labelStyle = miniLabelWhite;
						}
						else
						{
							if (contains)	//Going To Select
							{
								GUI.color = new Color(0f, 0.5f, 1f);
								labelStyle = miniLabelWhite;
							}
							else			//Not In Selection
							{
								GUI.color = Color.white;
								labelStyle = EditorStyles.miniLabel;
							}
						}

						GUI.Box(boxRect, GUIContent.none);
						GUI.color = Color.white;

						GUI.Label(new Rect(selectionPosition.x + 25, selectionPosition.y + i * h - h / 2f, 200 - 25, h), gameObject.name, labelStyle);


						if (contains && e.isMouse && e.type == EventType.MouseDown)
						{
							bool shift = e.shift;
							e.Use();
							MakeSelection(i, shift);
							//if (!shift)
							break;
						}
					}

					if (e.isKey && e.type == EventType.KeyUp)
					{
						switch (e.keyCode)
						{
							case KeyCode.Escape:
								
								EndSelection();
								e.Use();
								break;
							case KeyCode.Return:
								bool shift = e.shift;
								e.Use();
								if(indexCurrentlyHighlighted>=0)
									MakeSelection(indexCurrentlyHighlighted, shift);
								break;
						}
					}

					if (e.isMouse && e.type == EventType.MouseDown)
					{
						EndSelection();
						e.Use();
					}
				}
			}
			Handles.EndGUI();

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

				if (mouseRightIsDownWithoutDrag && e.rawType == EventType.MouseUp)
				{
					isShiftSelection = e.shift;
					totalSelection = GetAllOverlapping(e.mousePosition).ToArray();
					selectionPosition = e.mousePosition;
					e.alt = false;
					mouseRightIsDownWithoutDrag = false;
					RefreshSelectionHash();
					e.Use();
				}
			}
		}

		static void EndSelection()
		{
			totalSelection = null;
			currentSelectionHash.Clear();
		}

		static void RefreshSelectionHash()
		{
			currentSelectionHash.Clear();
			currentSelectionHash.UnionWith(Selection.gameObjects);
		}

		static void MakeSelection(int index, bool isShift)
		{
			GameObject gameObject = totalSelection[index];
			bool selectionContains = currentSelectionHash.Contains(gameObject);

			if (isShiftSelection || isShift)
			{
				HashSet<Object> objects = new HashSet<Object>(Selection.objects);
				if (!selectionContains)
					objects.Add(gameObject);
				else
					objects.Remove(gameObject);

				Selection.objects = objects.ToArray();
			}
			else
				Selection.activeGameObject = !selectionContains ? gameObject : null;

			if (!isShift)
				EndSelection();
			else
				RefreshSelectionHash();
			SceneView.RepaintAll();
		}
	}
}