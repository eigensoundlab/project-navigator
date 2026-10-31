// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Team-shared configuration: color tags, asset colors, rules, shared pins and notes.
    /// Saved to ProjectSettings/ProjectNavigator.asset, which is meant to be committed.
    /// </summary>
    [FilePath("ProjectSettings/ProjectNavigator.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class NavigatorSharedSettings : ScriptableSingleton<NavigatorSharedSettings>
    {
        public const int CurrentVersion = 2;
        /// <summary>Source of truth since 0.3.1; the .asset path above is only read for legacy data.</summary>
        public const string JsonPath = "ProjectSettings/ProjectNavigator.json";
        public const string LegacyAssetPath = "ProjectSettings/ProjectNavigator.asset";

        [SerializeField] int version;
        [SerializeField] bool defaultsCreated;

        public List<ColorTag> tags = new List<ColorTag>();
        public List<AssetColor> folderColors = new List<AssetColor>();   // name kept for file compatibility; holds files too
        public List<ColorRule> rules = new List<ColorRule>();
        public List<AssetPin> pins = new List<AssetPin>();
        public List<AssetNote> notes = new List<AssetNote>();
        /// <summary>Team-shared tool pins (a project's own editor tools); personal ones live in the global file.</summary>
        public List<ToolPin> toolPins = new List<ToolPin>();
        // Scene objects (keys are GlobalObjectId strings; the AssetColor/AssetNote "guid" field holds that id).
        public List<ScenePin> scenePins = new List<ScenePin>();
        public List<AssetColor> sceneColors = new List<AssetColor>();
        public List<AssetNote> sceneNotes = new List<AssetNote>();
        public bool rulesEnabled = false;

        /// <summary>Stable ids for the built-in tags, so static context-menu entries can reference them.</summary>
        public static class DefaultTags
        {
            public const string Yellow = "yellow";
            public const string Green = "green";
            public const string Blue = "blue";
            public const string Orange = "orange";
            public const string Red = "red";
            public const string Purple = "purple";
            public const string Teal = "teal";
            public const string Grey = "grey";

            public static readonly string[] All = { Yellow, Green, Blue, Orange, Red, Purple, Teal, Grey };
        }

        /// <summary>Data-format version of the loaded file; bump CurrentVersion when a migration is needed.</summary>
        public int Version => version;

        void OnEnable()
        {
            bool hadJson = NavigatorPersistence.Load(this, JsonPath); // JSON wins over whatever the legacy .asset provided
            if (!hadJson && !defaultsCreated && LegacyAssetImporter.TryImport(this, LegacyAssetPath, out _))
            {
                // Unity could not load the legacy file (its script GUID no longer resolves); parsed it directly instead.
                NavigatorPersistence.Save(this, JsonPath);
                Debug.Log("[Project Navigator] Recovered team settings from the legacy ProjectNavigator.asset file.");
            }
            if (defaultsCreated)
            {
                if (version < CurrentVersion) version = CurrentVersion; // migrations go here
                return;
            }
            CreateDefaults();
            defaultsCreated = true;
            version = CurrentVersion;
        }

        public void SaveNow()
        {
            NavigatorPersistence.Save(this, JsonPath);
        }

        public ColorTag FindTag(string id)
        {
            return string.IsNullOrEmpty(id) ? null : tags.Find(t => t.id == id);
        }

        public AssetColor FindColor(string guid)
        {
            return folderColors.Find(f => f.guid == guid);
        }

        public AssetPin FindPin(string guid)
        {
            return pins.Find(p => p.guid == guid);
        }

        public AssetNote FindNote(string guid)
        {
            return notes.Find(n => n.guid == guid);
        }

        /// <summary>Returns the built-in tag with this id, recreating it if the user deleted it.</summary>
        public ColorTag EnsureDefaultTag(string id)
        {
            var tag = FindTag(id);
            if (tag != null) return tag;
            tag = MakeDefaultTag(id);
            if (tag != null) tags.Add(tag);
            return tag;
        }

        public void ResetToDefaults()
        {
            folderColors.Clear();
            pins.Clear();
            notes.Clear();
            scenePins.Clear();
            sceneColors.Clear();
            sceneNotes.Clear();
            CreateDefaults();
        }

        void CreateDefaults()
        {
            tags = new List<ColorTag>();
            foreach (var id in DefaultTags.All)
                tags.Add(MakeDefaultTag(id));

            rules = new List<ColorRule>
            {
                new ColorRule { pattern = "**/Resources", tagId = DefaultTags.Orange },
                new ColorRule { pattern = "**/Editor", tagId = DefaultTags.Grey },
                new ColorRule { pattern = "**/StreamingAssets", tagId = DefaultTags.Purple },
                new ColorRule { pattern = "**/Plugins", tagId = DefaultTags.Grey },
                new ColorRule { pattern = "**/Scenes", tagId = DefaultTags.Green },
            };
            rulesEnabled = false;
        }

        static ColorTag MakeDefaultTag(string id)
        {
            switch (id)
            {
                case DefaultTags.Yellow: return Tag(id, "Yellow", "#E8C547");
                case DefaultTags.Green: return Tag(id, "Green", "#5DBB63");
                case DefaultTags.Blue: return Tag(id, "Blue", "#4A90E2");
                case DefaultTags.Orange: return Tag(id, "Orange", "#F0883A");
                case DefaultTags.Red: return Tag(id, "Red", "#D9534F");
                case DefaultTags.Purple: return Tag(id, "Purple", "#9B6BD6");
                case DefaultTags.Teal: return Tag(id, "Teal", "#2BB3A3");
                case DefaultTags.Grey: return Tag(id, "Grey", "#9A9A9A");
                default: return null;
            }
        }

        static ColorTag Tag(string id, string name, string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return new ColorTag { id = id, name = name, color = color };
        }
    }
}
