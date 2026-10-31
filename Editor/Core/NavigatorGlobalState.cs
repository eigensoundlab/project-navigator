// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>Data that is not project-specific: tool pins, window marks, recent windows, section layout.</summary>
    [Serializable]
    internal sealed class NavigatorGlobalData
    {
        public int version = 1;
        public List<ToolPin> toolPins = new List<ToolPin>();
        public List<WindowMark> windowMarks = new List<WindowMark>();
        public List<RecentWindow> recentWindows = new List<RecentWindow>();
        public List<SectionSetting> sections = new List<SectionSetting>();
        public SectionLayout dockedLayout = SectionLayout.Stacked;
        public SectionLayout floatingLayout = SectionLayout.Stacked;
        public string dockedActiveTab = "";
        public string floatingActiveTab = "";
        public bool trackWindows = true;
        public int recentWindowsCount = 10;
        public bool colorWindowTabs = true;
        public bool colorWindowStrips = true;
    }

    /// <summary>
    /// Per-user, cross-project storage (one JSON file in the user's application-data folder).
    /// Project data stays in ProjectSettings/UserSettings; this file holds what should follow the
    /// user between projects: favourite editor tools, window colors and labels, UI layout.
    /// </summary>
    internal static class NavigatorGlobal
    {
        static NavigatorGlobalData s_Data;

        public static string FilePath
        {
            get
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(root)) root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(root, "ProjectNavigator", "global.json");
            }
        }

        public static NavigatorGlobalData Data
        {
            get
            {
                if (s_Data == null) Load();
                return s_Data;
            }
        }

        static void Load()
        {
            s_Data = new NavigatorGlobalData();
            try
            {
                if (File.Exists(FilePath))
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), s_Data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Project Navigator] Could not read {FilePath}: {e.Message}");
            }
            NavigatorSections.EnsureDefaults(s_Data.sections);
        }

        public static void Save()
        {
            if (s_Data == null) return;
            try
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(FilePath, JsonUtility.ToJson(s_Data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Project Navigator] Could not save {FilePath}: {e.Message}");
            }
        }

        public static void MarkDirty()
        {
            Navigator.MarkDirty(global: true);
        }
    }
}
