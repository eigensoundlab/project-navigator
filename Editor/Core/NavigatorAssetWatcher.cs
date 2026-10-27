// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using UnityEditor;

namespace ProjectNavigator
{
    /// <summary>
    /// Keeps cached paths, names and colors in sync with the asset database.
    /// GUIDs survive renames and moves, so only deletions need pruning; everything else is a refresh.
    /// </summary>
    internal sealed class NavigatorAssetWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Length == 0 && deletedAssets.Length == 0 && movedAssets.Length == 0) return;

            FolderColorResolver.Invalidate();
            if (deletedAssets.Length > 0)
                Navigator.PruneMissing();
            Navigator.NotifyChanged();
        }
    }
}
