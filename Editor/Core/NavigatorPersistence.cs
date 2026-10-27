// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Saves and loads the two settings singletons as plain JSON next to the legacy .asset files.
    /// JSON carries no reference to the script that owns the data, so pins, colors and history survive
    /// package renames, moves between Assets/ and Packages/, and regenerated .meta GUIDs, which the
    /// Unity-serialized .asset files do not (a stale script GUID makes them load as empty).
    /// Legacy .asset files are still read once (when their script GUID still resolves) and then
    /// superseded by the JSON on the next save.
    /// </summary>
    internal static class NavigatorPersistence
    {
        public static string JsonPathFor(string assetPath)
        {
            return Path.ChangeExtension(assetPath, ".json");
        }

        public static bool Exists(string jsonPath)
        {
            return File.Exists(jsonPath);
        }

        public static void Save(UnityEngine.Object target, string jsonPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(jsonPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(jsonPath, EditorJsonUtility.ToJson(target, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Project Navigator] Could not save {jsonPath}: {e.Message}");
            }
        }

        /// <summary>Returns true when the file existed and was applied.</summary>
        public static bool Load(UnityEngine.Object target, string jsonPath)
        {
            if (!File.Exists(jsonPath)) return false;
            try
            {
                EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(jsonPath), target);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Project Navigator] Could not read {jsonPath}: {e.Message}");
                return false;
            }
        }
    }
}
