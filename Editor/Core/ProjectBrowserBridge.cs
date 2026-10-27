// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectNavigator
{
    /// <summary>
    /// Talks to the Project window using public APIs only.
    ///
    /// Entering a folder: Unity's own double-click path is ProjectBrowser.OpenSelectedFolders(),
    /// and AssetDatabase.OpenAsset on a folder asset calls back into that same method, acting on
    /// the folder currently selected in the browser's list. So: focus the Project window, select
    /// the folder (the browser frames it in its parent and selects it in the list), then call
    /// AssetDatabase.OpenAsset on the next editor tick. Two-column: shows the folder's contents.
    /// One-column: frames and expands the folder in the tree.
    ///
    /// The optional file ProjectBrowserBridge.Internals.cs adds a reflection-based path with direct
    /// access to the browser (exact folder entry and "which folder is being browsed" tracking).
    /// The Asset Store forbids reflection into editor internals (submission guidelines 2.5.g), so
    /// that file is simply removed for store builds; everything here keeps working without it.
    /// </summary>
    internal static partial class ProjectBrowserBridge
    {
        static bool s_Queried;
        static bool s_InternalsCanShowFolder;
        static bool s_InternalsCanReadActiveFolder;

        /// <summary>True when the optional internals file is present and resolved the folder-entry method.</summary>
        public static bool HasInternalsForNavigation
        {
            get
            {
                Query();
                return s_InternalsCanShowFolder;
            }
        }

        /// <summary>True when the optional internals file is present and can report the browsed folder.</summary>
        public static bool CanReadActiveFolder
        {
            get
            {
                Query();
                return s_InternalsCanReadActiveFolder;
            }
        }

        // Kept for callers/tests that only ask "can the tool enter folders at all": always yes.
        public static bool CanShowFolderContents => true;

        static void Query()
        {
            if (s_Queried) return;
            s_Queried = true;
            QueryInternals(ref s_InternalsCanShowFolder, ref s_InternalsCanReadActiveFolder);
        }

        /// <summary>Path of the folder the Project window is browsing. Only available with the internals file.</summary>
        public static bool TryGetActiveFolderPath(out string path)
        {
            path = null;
            if (!CanReadActiveFolder) return false;
            bool ok = false;
            TryGetActiveFolderPathInternal(ref path, ref ok);
            return ok && !string.IsNullOrEmpty(path);
        }

        /// <summary>Navigates the Project window into a folder and selects it.</summary>
        public static void ShowFolder(string folderPath)
        {
            var folder = AssetDatabase.LoadMainAssetAtPath(folderPath);
            if (folder == null) return;

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);

            bool handled = false;
            if (HasInternalsForNavigation)
                ShowFolderInternal(folder, ref handled);

            if (!handled)
            {
                // Public path. Deferred one tick so the browser has synced its list selection to the new
                // global selection before OpenAsset asks it to open the selected folders.
                EditorApplication.delayCall += () =>
                {
                    if (folder == null || Selection.activeObject != folder) return;
                    AssetDatabase.OpenAsset(folder);
                    EditorApplication.RepaintProjectWindow();
                };
            }

            EditorApplication.RepaintProjectWindow();
        }

        // Implemented in ProjectBrowserBridge.Internals.cs when that file is present; otherwise these
        // calls compile to nothing (C# partial methods).
        static partial void QueryInternals(ref bool canShowFolder, ref bool canReadActiveFolder);
        static partial void ShowFolderInternal(Object folder, ref bool handled);
        static partial void TryGetActiveFolderPathInternal(ref string path, ref bool ok);
    }
}
