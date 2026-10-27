// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    internal enum FolderClickAction
    {
        /// <summary>Single click selects and pings the folder; double-click opens it in the Project window.</summary>
        SelectThenDoubleClickOpens = 0,
        /// <summary>Single click opens the folder in the Project window.</summary>
        SingleClickOpens = 1
    }

    /// <summary>
    /// Personal data and preferences: history, frequency scores, personal pins, color and note overrides.
    /// Saved to UserSettings/ProjectNavigator.asset (per user, normally git-ignored).
    /// </summary>
    [FilePath("UserSettings/ProjectNavigator.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class NavigatorUserState : ScriptableSingleton<NavigatorUserState>
    {
        /// <summary>Source of truth since 0.3.1; the .asset path above is only read for legacy data.</summary>
        public const string JsonPath = "UserSettings/ProjectNavigator.json";
        public const string LegacyAssetPath = "UserSettings/ProjectNavigator.asset";

        void OnEnable()
        {
            bool hadJson = NavigatorPersistence.Load(this, JsonPath);
            if (!hadJson && IsEmpty && LegacyAssetImporter.TryImport(this, LegacyAssetPath, out _))
            {
                NavigatorPersistence.Save(this, JsonPath);
                Debug.Log("[Project Navigator] Recovered personal settings from the legacy ProjectNavigator.asset file.");
            }
        }

        public bool IsEmpty => pins.Count == 0 && folderColors.Count == 0 && notes.Count == 0 && history.Count == 0 && frecency.Count == 0;

        // ---- data ----
        public List<AssetPin> pins = new List<AssetPin>();
        public List<AssetColor> folderColors = new List<AssetColor>();   // name kept for file compatibility; holds files too
        public List<AssetNote> notes = new List<AssetNote>();
        public HistoryStack history = new HistoryStack();                 // folders only (back/forward)
        public List<FrecencyEntry> frecency = new List<FrecencyEntry>();  // folders
        public List<FrecencyEntry> fileFrecency = new List<FrecencyEntry>();

        // ---- history preferences ----
        public int maxHistory = 200;
        public float halfLifeDays = 7f;
        public bool recordParentFolderOfSelectedAssets = true;
        public bool ignorePackages = true;
        public float pollIntervalSeconds = 0.25f;
        /// <summary>Also remember selected files (shown as "Recent files"; folder back/forward is unaffected).</summary>
        public bool trackFiles = false;

        // ---- Project window / Inspector ----
        public bool overlayEnabled = true;
        public bool showPinBadge = true;
        public bool tintFilesInColoredFolders = true;
        public bool showNotesInProjectWindow = true;
        public bool inspectorStripEnabled = true;
        public bool inspectorStripForFiles = false;

        /// <summary>When true, colors and notes set from menus / the inspector go to the team-shared settings.</summary>
        public bool colorScopeShared = true;

        // ---- window ----
        /// <summary>How a folder row in the Navigator reacts to clicks.</summary>
        public FolderClickAction folderClickAction = FolderClickAction.SelectThenDoubleClickOpens;
        public bool compactMode = false;
        public int frequentCount = 10;
        public int recentCount = 20;
        public int recentFilesCount = 10;

        // ---- floating window ----
        public bool floatingOnTop = true;
        public bool reopenFloatingOnStartup = true;
        public bool floatingWasOpen = false;
        public bool floatingCompact = true;
        public Rect floatingRect = new Rect(120f, 120f, 320f, 420f);

        public void SaveNow()
        {
            NavigatorPersistence.Save(this, JsonPath);
        }
    }
}
