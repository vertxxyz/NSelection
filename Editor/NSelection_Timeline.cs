using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
// ReSharper disable UseNegatedPatternInIsExpression

#if UNITY_TIMELINE
namespace Vertx
{
	public partial class NSelection
	{
		/// <summary>
		/// Sets the hierarchy's expanded state to only contain the current selection.
		/// </summary>
		public static void FocusTimelineWindowToSelection()
		{
			TimelineAsset asset = TimelineEditor.inspectedAsset;
			if (asset == null)
				return;
			
			TimelineClip[] selectedClips = TimelineEditor.selectedClips;
			HashSet<TrackAsset> selectedTracks = new HashSet<TrackAsset>();
			foreach (TimelineClip clip in selectedClips)
				selectedTracks.Add(clip.GetParentTrack());
			Object[] selectedObjects = Selection.objects;
			foreach (Object o in selectedObjects)
			{
				if (o is TrackAsset track)
					selectedTracks.Add(track);
			}

			HashSet<TrackAsset> parents = new HashSet<TrackAsset>();
			foreach (TrackAsset track in selectedTracks)
				CollectParents(track, parents);
			SetExpandedStates(asset, parents);

			TimelineEditorWindow window = TimelineEditor.GetWindow();
			object treeView = window.GetType().GetProperty("treeView", BindingFlags.Public | BindingFlags.Instance).GetValue(window);
			treeView.GetType().GetMethod("Reload", BindingFlags.Public | BindingFlags.Instance).Invoke(treeView, null);
		}

		private static void CollectParents(TrackAsset track, HashSet<TrackAsset> result)
		{
			while (track.parent != null && !(track.parent is TimelineAsset))
			{
				track = (TrackAsset)track.parent;
				if (!result.Add(track))
					return;
			}
		}

		private static void SetExpandedStates(TimelineAsset asset, HashSet<TrackAsset> expanded)
		{
			foreach (TrackAsset track in asset.GetRootTracks())
				SetExpandedStates(track, expanded);
		}
		
		private static void SetExpandedStates(TrackAsset asset, HashSet<TrackAsset> expanded)
		{
			asset.SetCollapsed(!expanded.Contains(asset));
			foreach (TrackAsset track in asset.GetChildTracks())
				SetExpandedStates(track, expanded);
		}
	}
}
#endif