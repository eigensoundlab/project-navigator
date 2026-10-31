// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace ProjectNavigator
{
    /// <summary>The Navigator window's sections, their titles and default visibility per window kind and mode.</summary>
    internal static class NavigatorSections
    {
        public const string Pinned = "pinned";
        public const string Scene = "scene";
        public const string Frequent = "frequent";
        public const string Recent = "recent";
        public const string RecentFiles = "recentFiles";
        public const string Colored = "colored";
        public const string Tools = "tools";
        public const string Windows = "windows";

        public static readonly string[] All = { Pinned, Scene, Frequent, Recent, RecentFiles, Colored, Tools, Windows };

        public static string Title(string id)
        {
            switch (id)
            {
                case Pinned: return "Pinned";
                case Scene: return "Scene";
                case Frequent: return "Frequent";
                case Recent: return "Recent";
                case RecentFiles: return "Recent files";
                case Colored: return "Colored";
                case Tools: return "Tools";
                case Windows: return "Windows";
                default: return id;
            }
        }

        public static SectionSetting Default(string id)
        {
            var s = new SectionSetting { id = id, docked = true, floating = true };
            switch (id)
            {
                case Pinned:
                    s.dockedCompact = true;
                    s.floatingCompact = true;
                    break;
                case Tools:
                    s.dockedCompact = true;
                    s.floatingCompact = true;
                    break;
                case Colored:
                    s.floating = false;
                    break;
                case Windows:
                    s.floating = true;
                    break;
            }
            return s;
        }

        /// <summary>Adds missing sections with defaults (next to their default neighbours), drops unknown ids, keeps the stored order.</summary>
        public static void EnsureDefaults(List<SectionSetting> list)
        {
            list.RemoveAll(s => s == null || System.Array.IndexOf(All, s.id) < 0);
            for (int i = 0; i < All.Length; i++)
            {
                var id = All[i];
                if (list.Exists(s => s.id == id)) continue;
                int insertAt = list.Count;
                if (i > 0)
                {
                    int previous = list.FindIndex(s => s.id == All[i - 1]);
                    if (previous >= 0) insertAt = previous + 1;
                }
                list.Insert(insertAt, Default(id));
            }
        }

        public static bool IsVisible(SectionSetting s, bool floating, bool compact)
        {
            return floating ? (compact ? s.floatingCompact : s.floating) : (compact ? s.dockedCompact : s.docked);
        }

        public static void SetVisible(SectionSetting s, bool floating, bool compact, bool value)
        {
            if (floating)
            {
                if (compact) s.floatingCompact = value;
                else s.floating = value;
            }
            else
            {
                if (compact) s.dockedCompact = value;
                else s.docked = value;
            }
        }
    }
}
