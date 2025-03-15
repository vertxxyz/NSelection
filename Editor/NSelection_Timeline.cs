#if UNITY_TIMELINE
#if !UNITY_TIMELINE_1_5_2
using System;
using System.Reflection;
#endif
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

// ReSharper disable UseNegatedPatternInIsExpression

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
			{
#if UNITY_TIMELINE_1_5_2
				selectedTracks.Add(clip.GetParentTrack());
#else
				selectedTracks.Add(clip.parentTrack);
#endif
			}

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

#if UNITY_TIMELINE_1_5_2
			EditorWindow window = TimelineEditor.GetWindow();
#else
			EditorWindow window =
				(EditorWindow)typeof(TimelineEditor).GetProperty("window", NonPublicStatic).GetValue(null);
#endif
			object treeView = window.GetType().GetProperty("treeView", PublicInstance)!.GetValue(window);
			treeView.GetType().GetMethod("Reload", PublicInstance)!.Invoke(treeView, null);
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

#if !UNITY_TIMELINE_1_5_2
		private static MethodInfo _setTrackCollapsed;

		private static MethodInfo setTrackCollapsed
			=> _setTrackCollapsed ??
			   (
				   _setTrackCollapsed =
					   Type.GetType("UnityEditor.Timeline.TimelineWindowViewPrefs,Unity.Timeline.Editor")
						   .GetMethod("SetTrackCollapsed", PublicStatic)
			   );

		private static void SetTrackCollapsed(TrackAsset track, bool collapsed) =>
			setTrackCollapsed.Invoke(null, new object[] { track, collapsed });
#endif

		private static void SetExpandedStates(TrackAsset asset, HashSet<TrackAsset> expanded)
		{
#if UNITY_TIMELINE_1_5_2
			asset.SetCollapsed(!expanded.Contains(asset));
#else
			SetTrackCollapsed(asset, !expanded.Contains(asset));
#endif
			foreach (TrackAsset track in asset.GetChildTracks())
				SetExpandedStates(track, expanded);
		}
	}
}
#endif