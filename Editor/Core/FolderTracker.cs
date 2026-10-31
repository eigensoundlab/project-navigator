// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace ProjectNavigator
{
    /// <summary>
    /// Records visits whether or not the Navigator window is open:
    ///  1. Selection changes: a selected folder, the containing folder of a selected asset, and
    ///     (when "track files" is on) the selected file itself.
    ///  2. The folder the Project window is browsing (two-column layout), polled a few times per second.
    /// Folder visits feed <see cref="Navigator.RecordVisit"/>, which ignores repeats of the current folder.
    /// </summary>
    [InitializeOnLoad]
    internal static class FolderTracker
    {
        static double s_NextPoll;
        static string s_LastPolledPath;

        static FolderTracker()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        static void OnSelectionChanged()
        {
            var guids = Selection.assetGUIDs;
            if (guids == null || guids.Length != 1) return;

            var guid = guids[0];
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            if (AssetDatabase.IsValidFolder(path))
            {
                Navigator.RecordVisit(guid, VisitSource.Selection);
                return;
            }

            var user = NavigatorUserState.instance;
            if (user.trackFiles)
                Navigator.RecordFileVisit(guid);

            if (!user.recordParentFolderOfSelectedAssets) return;

            var parent = Navigator.ParentOf(path);
            if (string.IsNullOrEmpty(parent)) return;
            var parentGuid = Navigator.GuidOf(parent);
            if (!string.IsNullOrEmpty(parentGuid))
                Navigator.RecordVisit(parentGuid, VisitSource.Selection);
        }

        static void Poll()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < s_NextPoll) return;
            s_NextPoll = now + Mathf.Max(0.1f, NavigatorUserState.instance.pollIntervalSeconds);
            if (!ProjectBrowserBridge.CanReadActiveFolder) return;

            Profiler.BeginSample("ProjectNavigator.PollActiveFolder");
            try
            {
                PollInner();
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        static void PollInner()
        {
            if (!ProjectBrowserBridge.TryGetActiveFolderPath(out var path)) return;
            if (path == s_LastPolledPath) return;
            s_LastPolledPath = path;

            // The browser reports the root while searching or in the one-column layout; the root is
            // rarely a destination worth remembering, and explicit selection still records it.
            if (path == "Assets" || path == "Packages") return;
            if (!AssetDatabase.IsValidFolder(path)) return;

            var guid = Navigator.GuidOf(path);
            if (!string.IsNullOrEmpty(guid))
                Navigator.RecordVisit(guid, VisitSource.ActiveFolder);
        }
    }
}
