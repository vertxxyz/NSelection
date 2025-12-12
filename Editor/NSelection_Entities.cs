#if UNITY_ENTITIES
using System;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Vertx.Selection.Editor
{
	public partial class NSelection
	{
		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		/// <param name="focusedWindow"></param>
		public static void FocusEntitiesHierarchyToSelection(EditorWindow focusedWindow)
		{
			// Unity.Entities.Editor.HierarchyMultiColumnListView
			var hierarchy = focusedWindow.rootVisualElement.Q<MultiColumnListView>();

			int[] selection = hierarchy.selectedIds.ToArray();

			if (selection.Length == 0)
			{
				hierarchy.ClearSelection();
				return;
			}

			// Unity.Entities.Editor.Hierarchy
			object model = hierarchy.GetType().GetField("m_Model", NonPublicInstance)!.GetValue(hierarchy);
			// Unity.Entities.Editor.HierarchyNodes
			object hierarchyNodes = model.GetType().GetField("m_HierarchyNodes", NonPublicInstance)!.GetValue(model);

			Type hierarchyNodesType = hierarchyNodes.GetType();
			// HierarchyNode.Immutable
			object hierarchyNode = hierarchyNodesType.GetMethod(
				"get_Item",
				PublicInstance,
				null,
				new[] { typeof(int) },
				null
			)!.Invoke(hierarchyNodes, new object[] { selection[0] });
			object hierarchyNodeHandle = hierarchyNode.GetType().GetMethod("GetHandle", PublicInstance)!.Invoke(hierarchyNode, null);
			
			hierarchy.ClearSelection();
			// "Collapse all"
			hierarchyNodesType.GetMethod("Clear", PublicInstance)!.Invoke(hierarchyNodes, null);

			// Set expanded/selection
			Type.GetType("Unity.Entities.Editor.HierarchyWindow,Unity.Entities.Editor")!
				.GetMethod("SelectHierarchyNode", PublicStatic)!
				.Invoke(null,
					new[]
					{
						model,
						hierarchyNodeHandle,
						2
					}
				);
		}
	}
}
#endif