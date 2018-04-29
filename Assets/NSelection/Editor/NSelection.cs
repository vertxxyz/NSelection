/*Thomas Ingram 2018*/

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

		private static readonly int id = "NSelection".GetHashCode();

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
					SelectionPopup.isShiftSelection = e.shift;
					SelectionPopup.totalSelection = GetAllOverlapping(e.mousePosition).ToArray();

					SelectionPopup.objectToComponentIcons.Clear();
					foreach (GameObject gO in SelectionPopup.totalSelection)
					{
						Component[] cS = gO.GetComponents<Component>();
						Texture2D[] icons = new Texture2D[cS.Length - 1];
						for (var i = 1; i < cS.Length; i++)
						{
							//Skip the Transform component because it's always the first object
							icons[i - 1] = AssetPreview.GetMiniThumbnail(cS[i]);
						}

						SelectionPopup.objectToComponentIcons.Add(gO, icons);
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
							sceneView.position.y + e.mousePosition.y - height / 2f,
							width,
							height * SelectionPopup.totalSelection.Length + 1
							)
						);
					if (popup == null)
					{
						e.Use();
						return;
					}


					e.alt = false;
					mouseRightIsDownWithoutDrag = false;
					SelectionPopup.RefreshSelectionHash();
					//------------------

					e.Use();
				}
			}
		}

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
	}

	public class SelectionPopup : EditorWindow
	{
		public static GameObject[] totalSelection;

		private static readonly HashSet<GameObject> currentSelectionHash = new HashSet<GameObject>();
		public static readonly Dictionary<GameObject, Texture2D[]> objectToComponentIcons = new Dictionary<GameObject, Texture2D[]>();

		public static bool isShiftSelection;

//		private static Vector2 selectionPosition;
		private static float xOffset;
		public static float scrollPosition;
		private const int maxIcons = 7;

		private static Color boxBorderColor
		{
			get { return new Color(0, 0, 0, 1); }
		}

		private static readonly Texture2D shurikenPlus = EditorGUIUtility.FindTexture("ShurikenPlus");


		//Mini white label is used for highlighted content
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

		public static SelectionPopup ShowModal(Rect r)
		{
			if (totalSelection == null || totalSelection.Length == 0)
				return null;
			SelectionPopup popup = CreateInstance<SelectionPopup>();
			popup.ShowAsDropDown(r, r.size);
			return popup;
		}


		private void OnEnable()
		{
			SceneView.onSceneGUIDelegate += CaptureEvents;
				
		}

		private void OnDisable()
		{
			SceneView.onSceneGUIDelegate -= CaptureEvents;
		}

		void CaptureEvents(SceneView sceneView)
		{
			Event e = Event.current;
			if (e.type == EventType.KeyDown || e.type == EventType.KeyUp)
				e.Use();
		}

		void OnGUI()
		{
			Event e = Event.current;

			//EditorGUIUtility.AddCursorRect(new Rect(0f, 17f, Screen.width, Screen.height - 17f), isShiftSelection || e.shift ? MouseCursor.ArrowPlus : MouseCursor.Arrow);
			int indexCurrentlyHighlighted = -1;

			//Scrolling behaviour
			if (e.type == EventType.ScrollWheel)
			{
				scrollPosition += Math.Sign(e.delta.y);
				scrollPosition = Mathf.Clamp(scrollPosition, 0, totalSelection.Length - 1);
				e.Use();
			}

			Rect seperatorTopRect = new Rect(0, 0, NSelection.width, 1);
			EditorGUI.DrawRect(seperatorTopRect, boxBorderColor);

			for (var i = 0; i < totalSelection.Length; i++)
			{
				GameObject gameObject = totalSelection[i];

				Rect boxRect = new Rect(0, i * NSelection.height, NSelection.width, NSelection.height);
				GUIStyle labelStyle;

				bool isInSelection = currentSelectionHash.Contains(gameObject);
				bool contains = boxRect.Contains(e.mousePosition);
				if (contains)
					indexCurrentlyHighlighted = i;

				EditorGUI.DrawRect(boxRect, boxBorderColor);

				if (isInSelection)
				{
					if (contains) //Going to Deselect
						GUI.color = new Color(0.58f, 0.62f, 0.75f);
					else //Is In Selection
						GUI.color = new Color(0f, 0.5f, 1f);
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
						labelStyle = EditorStyles.miniLabel;
					}
				}

				Rect innerBoxRect = new Rect(boxRect.x + 1, boxRect.y, boxRect.width - 2, boxRect.height - 1);
				EditorGUI.DrawRect(innerBoxRect, GUI.color);
				GUI.color = Color.white;
				GUI.Label(new Rect(boxRect.x + 20, boxRect.y, NSelection.width - 20, boxRect.height), gameObject.name, labelStyle);

				Texture2D[] icons;
				if (objectToComponentIcons.TryGetValue(gameObject, out icons))
				{
					int maxLength = icons.Length;
					bool overMaxLength = icons.Length > maxIcons;
					if (overMaxLength)
						maxLength = maxIcons;
					for (var j = 0; j < maxLength; j++)
					{
						Texture2D icon = icons[j];
						if (overMaxLength && j >= maxLength - 1)
						{
							GUI.Label(new Rect(boxRect.x + boxRect.width - (maxLength - j) * NSelection.height, boxRect.y, NSelection.height, NSelection.height), shurikenPlus);
						}
						else
						{
							GUI.Label(new Rect(boxRect.x + boxRect.width - (maxLength - j) * NSelection.height, boxRect.y, NSelection.height, NSelection.height), icon);
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


			if (e.isKey && e.type == EventType.KeyUp)
			{
				switch (e.keyCode)
				{
					case KeyCode.Escape:
						EndSelection();
						break;
					case KeyCode.Return:
						if (indexCurrentlyHighlighted >= 0)
							MakeSelection(indexCurrentlyHighlighted, e.shift);
						break;
				}
			}
			else if (e.isMouse && e.type == EventType.MouseUp)
				EndSelection();

			if (e.type != EventType.Repaint && e.type != EventType.Layout)
				e.Use();
			
			Repaint();
		}

		void EndSelection()
		{
			scrollPosition = 0;
			totalSelection = null;
			currentSelectionHash.Clear();
			GUIUtility.hotControl = 0;
			SceneView.RepaintAll();
			Close();
		}

		public static void RefreshSelectionHash()
		{
			currentSelectionHash.Clear();
			currentSelectionHash.UnionWith(Selection.gameObjects);
		}

		void MakeSelection(int index, bool isShift)
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
				isShiftSelection = true;
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