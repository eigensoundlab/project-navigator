// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>What the overlay and the window need to know about one asset GUID.</summary>
    internal sealed class FolderStyle
    {
        public static readonly FolderStyle None = new FolderStyle();

        public bool isFolder;
        public bool hasColor;
        /// <summary>True when a file carries its own explicit color (drawn stronger than a folder-inherited tint).</summary>
        public bool explicitOnFile;
        public Color color;
        public string tagId;
        public bool tintFiles;
        public bool pinned;
        public Texture icon;
        public string name;
        public string note;

        // Overlay caches (measured on first draw, GUI context required).
        public GUIContent noteContent;
        public float nameWidth = -1f;

        public bool HasNote => !string.IsNullOrEmpty(note);
        public bool Visible => hasColor || pinned || HasNote;
    }

    /// <summary>
    /// Resolves the color of a folder (or of a file, through its folder) with this precedence:
    ///   1. explicit assignment on the asset itself (personal override beats team),
    ///   2. first enabled rule whose glob matches the folder path (when rules are enabled),
    ///   3. nearest ancestor with an explicit assignment whose tag has "inherit to children".
    /// An explicit "none" ("" tag) on a folder gives it no color and stops inheritance below it.
    /// Files without their own color take the folder's tag when the tag has "tint files" enabled.
    /// Results are cached per GUID; the cache is cleared whenever settings or assets change.
    /// </summary>
    internal static class FolderColorResolver
    {
        static readonly Dictionary<string, FolderStyle> s_Cache = new Dictionary<string, FolderStyle>();

        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.projectChanged -= Invalidate;
            EditorApplication.projectChanged += Invalidate;
        }

        public static void Invalidate()
        {
            s_Cache.Clear();
            GlobMatcher.ClearCache();
            EditorApplication.RepaintProjectWindow();
        }

        /// <summary>Returns true when there is something to draw for this GUID (a color, a pin badge or a note).</summary>
        public static bool TryGet(string guid, out FolderStyle style)
        {
            if (string.IsNullOrEmpty(guid))
            {
                style = FolderStyle.None;
                return false;
            }
            if (!s_Cache.TryGetValue(guid, out style))
            {
                style = Resolve(guid);
                s_Cache[guid] = style;
            }
            return style.Visible;
        }

        static FolderStyle Resolve(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return FolderStyle.None;

            var shared = NavigatorSharedSettings.instance;
            var user = NavigatorUserState.instance;
            bool isFolder = AssetDatabase.IsValidFolder(path);

            string tagId;
            bool explicitOnFile = false;
            if (isFolder)
            {
                tagId = ResolveTag(path, guid, shared, user);
            }
            else if (TryExplicit(guid, shared, user, out var fileTag))
            {
                tagId = NullIfEmpty(fileTag);
                explicitOnFile = tagId != null;
            }
            else
            {
                var parent = Navigator.ParentOf(path);
                var parentGuid = Navigator.GuidOf(parent);
                tagId = string.IsNullOrEmpty(parentGuid) ? null : ResolveTag(parent, parentGuid, shared, user);
            }

            var style = new FolderStyle
            {
                isFolder = isFolder,
                tagId = tagId,
                pinned = Navigator.IsPinned(guid),
                name = Navigator.NameOf(path),
                note = Navigator.GetNote(guid)
            };

            var tag = shared.FindTag(tagId);
            if (tag != null)
            {
                bool inheritedFileTint = !isFolder && !explicitOnFile && tag.tintFiles && user.tintFilesInColoredFolders;
                if (isFolder || explicitOnFile || inheritedFileTint)
                {
                    style.hasColor = true;
                    style.color = tag.color;
                    style.explicitOnFile = explicitOnFile;
                    style.tintFiles = inheritedFileTint;
                }
            }

            if (style.hasColor && isFolder)
                style.icon = AssetDatabase.GetCachedIcon(path);
            if (style.HasNote)
                style.noteContent = new GUIContent(style.note, style.note);

            return style;
        }

        internal static string ResolveTag(string folderPath, string folderGuid, NavigatorSharedSettings shared, NavigatorUserState user)
        {
            // 1. explicit on this folder
            if (TryExplicit(folderGuid, shared, user, out var explicitTag))
                return NullIfEmpty(explicitTag);

            // 2. rules
            if (shared.rulesEnabled)
            {
                foreach (var rule in shared.rules)
                {
                    if (!rule.enabled || string.IsNullOrEmpty(rule.tagId)) continue;
                    if (GlobMatcher.IsMatch(rule.pattern, folderPath)) return rule.tagId;
                }
            }

            // 3. inherited from the nearest explicitly colored ancestor
            var parent = Navigator.ParentOf(folderPath);
            while (!string.IsNullOrEmpty(parent))
            {
                var parentGuid = Navigator.GuidOf(parent);
                if (!string.IsNullOrEmpty(parentGuid) && TryExplicit(parentGuid, shared, user, out var ancestorTag))
                {
                    if (string.IsNullOrEmpty(ancestorTag)) return null; // explicit "none" stops inheritance
                    var tag = shared.FindTag(ancestorTag);
                    return tag != null && tag.inheritToChildren ? ancestorTag : null;
                }
                parent = Navigator.ParentOf(parent);
            }

            return null;
        }

        static bool TryExplicit(string guid, NavigatorSharedSettings shared, NavigatorUserState user, out string tagId)
        {
            var entry = user.folderColors.Find(f => f.guid == guid) ?? shared.folderColors.Find(f => f.guid == guid);
            tagId = entry?.tagId;
            return entry != null;
        }

        static string NullIfEmpty(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }
}
