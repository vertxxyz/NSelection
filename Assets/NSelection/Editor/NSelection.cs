using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;
using System.Linq;

namespace Vertx
{
	[InitializeOnLoad]
	public class NSelection {

		#region Overlapping
		static IEnumerable<GameObject> GetAllOverlapping (Vector2 position) { 
			return (IEnumerable<GameObject>)getAllOverlapping.Invoke (null, new object[]{ position });
		}

		[SerializeField]
		static MethodInfo _getAllOverlapping;
		static MethodInfo getAllOverlapping {
			get {
				if (_getAllOverlapping == null)
					_getAllOverlapping = sceneViewPickingClass.GetMethod ("GetAllOverlapping", BindingFlags.Static | BindingFlags.NonPublic);
				return _getAllOverlapping;
			}
		}

		[SerializeField]
		static Type _sceneViewPickingClass;
		static Type sceneViewPickingClass {
			get {
				if (_sceneViewPickingClass == null)
					_sceneViewPickingClass = Type.GetType ("UnityEditor.SceneViewPicking,UnityEditor");
				return _sceneViewPickingClass;
			}
		}
		#endregion

		#region CustomMenu
		static void DisplayCustomMenu (Rect r, string[] menuNames, int[] selected, EditorUtility.SelectMenuItemFunction callback, object userData){
			displayCustomMenu.Invoke (null, new object[]{ r, menuNames, selected, callback, userData });
		}

		[SerializeField]
		static MethodInfo _displayCustomMenu;
		static MethodInfo displayCustomMenu {
			get {
				if (_displayCustomMenu == null)
					_displayCustomMenu = typeof(EditorUtility).GetMethod ("DisplayCustomMenu", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[]{typeof(Rect), typeof(string[]), typeof(int[]), typeof(EditorUtility.SelectMenuItemFunction), typeof(object)}, null);
				return _displayCustomMenu;
			}
		}
		#endregion

		static NSelection () {
			RefreshListeners ();
		}

		static void RefreshListeners () {
			SceneView.onSceneGUIDelegate -= OnSceneGUI;
			SceneView.onSceneGUIDelegate += OnSceneGUI;
		}

		static bool mouseRightIsDownWithoutDrag = false;

		static void OnSceneGUI (SceneView sceneView) {
			
			Event e = Event.current;

			if (e.type == EventType.Repaint) {
				//If lastDisplayAction is not null, we're looking to re-display the menu if shift or control is held.
				if (lastDisplaySameCustomMenuAction != null) {
					if (e.shift || e.control) {
						//If we hold shift or control then we want to display again.
						lastDisplaySameCustomMenuAction.Invoke ();
						return;
					} else {
						//Otherwise we wish to hide the menu.
						lastDisplaySameCustomMenuAction = null;
					}
				}
			}


			if (e.control && e.isMouse && e.button == 1){
				if(e.rawType == EventType.MouseDown)
					mouseRightIsDownWithoutDrag = true;
				if (e.rawType == EventType.MouseDrag)
					mouseRightIsDownWithoutDrag = false;

				if(mouseRightIsDownWithoutDrag && e.rawType == EventType.MouseUp) {
					GameObject[] selecting = GetAllOverlapping (e.mousePosition).ToArray ();
					Vector2 mousePos = e.mousePosition;
					Rect r = new Rect (mousePos.x, mousePos.y, 0, 0);

					displaySameCustomMenuAction = ()=>{
						//We need to set lastDisplayAction to null because it will only be set for reuse if we've selected an item in the context menu.
						lastDisplaySameCustomMenuAction = null;
						string[] selectingNames = new string[selecting.Length];
						List<int> selected = new List<int> ();
						GameObject[] selectedGOs = Selection.gameObjects;
						for (int i = 0; i < selecting.Length; i++) {
							selectingNames [i] = selecting [i].name;
							if (selectedGOs.Contains (selecting [i]))
								selected.Add (i);
						}
						DisplayCustomMenu(r, selectingNames, selected.ToArray(), new EditorUtility.SelectMenuItemFunction(ContextMenuDelegate), selecting);
					};

					e.alt = false;
					displaySameCustomMenuAction.Invoke ();
					mouseRightIsDownWithoutDrag = false;
					e.Use ();
				}
			}
		}

		static Action displaySameCustomMenuAction;
		static Action lastDisplaySameCustomMenuAction;

		static void ContextMenuDelegate (object userData, string[] options, int selected){
			GameObject[] selecting = (GameObject[])userData;
			List<GameObject> selectedGOs = new List<GameObject>(Selection.gameObjects);
			if(!selectedGOs.Remove(selecting [selected])){
				selectedGOs.Add (selecting [selected]);
			}
			Selection.objects = selectedGOs.ToArray ();
			//Set the lastDisplayAction to the displayAction to signal we want to re-display this menu.
			lastDisplaySameCustomMenuAction = displaySameCustomMenuAction;
		}
	}
}