// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectNavigator
{
    /// <summary>A named color that any number of folders or files can reference.</summary>
    [Serializable]
    internal sealed class ColorTag
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "New tag";
        public Color color = new Color(0.91f, 0.77f, 0.28f, 1f);
        /// <summary>Sub-folders without an explicit color (or rule match) take this color.</summary>
        public bool inheritToChildren = true;
        /// <summary>Files inside a folder with this color get a small colored bar.</summary>
        public bool tintFiles = false;
    }

    /// <summary>
    /// Explicit color on one asset (folder or file), by GUID so renames and moves survive.
    /// An empty <see cref="tagId"/> means "explicitly no color": it blocks rules and inheritance.
    /// </summary>
    [Serializable]
    internal sealed class AssetColor
    {
        [FormerlySerializedAs("folderGuid")] public string guid = "";
        public string tagId = "";
    }

    /// <summary>Glob rule evaluated against a folder's asset path, e.g. "**/Resources" or "Assets/Art/**".</summary>
    [Serializable]
    internal sealed class ColorRule
    {
        public string pattern = "**/Resources";
        public string tagId = "";
        public bool enabled = true;
    }

    /// <summary>A pinned folder or file. Label and group are optional display helpers; the asset itself is not renamed.</summary>
    [Serializable]
    internal sealed class AssetPin
    {
        [FormerlySerializedAs("folderGuid")] public string guid = "";
        public string label = "";
        public string group = "";
    }

    /// <summary>A short free-text note attached to a folder or file (shown next to its path).</summary>
    [Serializable]
    internal sealed class AssetNote
    {
        public string guid = "";
        public string text = "";
    }

    /// <summary>Decayed visit counter used to rank "Frequent" folders and "Recent files".</summary>
    [Serializable]
    internal sealed class FrecencyEntry
    {
        public string guid = "";
        public double score;
        public long lastVisitTicks;
    }

    /// <summary>Resolved view of an asset GUID. Not serialized.</summary>
    internal sealed class AssetInfo
    {
        public string guid;
        public string path;
        public string name;
        public bool isFolder;
        public bool Exists => !string.IsNullOrEmpty(path);
    }
}

namespace ProjectNavigator
{
    internal enum ToolPinKind
    {
        MenuItem = 0,
        Window = 1
    }

    /// <summary>A pinned editor tool: a menu item (any Unity menu path) or an editor window type.</summary>
    [Serializable]
    internal sealed class ToolPin
    {
        public string id = Guid.NewGuid().ToString("N");
        public ToolPinKind kind;
        /// <summary>Menu path such as "Window/Rendering/Lighting" (menu-item pins).</summary>
        public string menuPath = "";
        /// <summary>Assembly-qualified type name of the EditorWindow (window pins).</summary>
        public string windowType = "";
        public string label = "";
        public string group = "";
        public bool hasColor;
        public Color color = Color.white;
    }

    /// <summary>Label and color for an open editor window, by instance (type|title|ordinal) or by type.</summary>
    [Serializable]
    internal sealed class WindowMark
    {
        public string key = "";
        public bool byType;
        public string label = "";
        public bool hasColor;
        public Color color = Color.white;
    }

    [Serializable]
    internal sealed class RecentWindow
    {
        public string windowType = "";
        public string title = "";
        public long lastFocusTicks;
    }

    internal enum SectionLayout
    {
        Stacked = 0,
        Tabs = 1
    }

    /// <summary>Visibility of one Navigator section in each window kind and mode, plus its position in the order.</summary>
    [Serializable]
    internal sealed class SectionSetting
    {
        public string id = "";
        public bool docked = true;
        public bool dockedCompact;
        public bool floating = true;
        public bool floatingCompact;
    }
}
