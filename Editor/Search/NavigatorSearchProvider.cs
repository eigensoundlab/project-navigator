// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Unity Search provider ("nav:") that ranks every project folder, plus pinned and recently used
    /// files, by fuzzy match (name, path and note), pin status and frequency of use.
    /// Alt+Shift+F opens the Search window scoped to this provider; Enter opens the result.
    /// Also usable from Ctrl+K by typing "nav:".
    /// </summary>
    internal static class NavigatorSearchProvider
    {
        public const string Id = "projnav";
        const string k_FilterId = "nav:";

        struct Entry
        {
            public string guid;
            public string path;
            public string name;
            public bool isFolder;
        }

        static List<Entry> s_FolderIndex;

        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.projectChanged -= InvalidateIndex;
            EditorApplication.projectChanged += InvalidateIndex;
        }

        static void InvalidateIndex()
        {
            s_FolderIndex = null;
        }

        public static void ShowQuickJump()
        {
            SearchService.ShowContextual(Id);
        }

        [SearchItemProvider]
        static SearchProvider CreateProvider()
        {
            return new SearchProvider(Id, "Navigator")
            {
                filterId = k_FilterId,
                priority = 55,
                isExplicitProvider = true,
                fetchItems = FetchItems,
                fetchLabel = (item, context) => item.label,
                fetchDescription = (item, context) => item.description,
                fetchThumbnail = (item, context) => AssetDatabase.GetCachedIcon((string)item.data) as Texture2D,
                toObject = (item, type) => AssetDatabase.LoadAssetAtPath((string)item.data, type)
            };
        }

        [SearchActionsProvider]
        static IEnumerable<SearchAction> CreateActions()
        {
            yield return new SearchAction(Id, "open", null, "Open (folder: show in Project window, file: select and ping)", (SearchItem item) => Navigator.Open(item.id));
            yield return new SearchAction(Id, "openasset", null, "Open asset (scene, prefab, script, external app)", (SearchItem item) => Navigator.OpenAsset(item.id));
            yield return new SearchAction(Id, "pin", null, "Pin", (SearchItem item) => Navigator.Pin(item.id, false));
        }

        static object FetchItems(SearchContext context, List<SearchItem> items, SearchProvider provider)
        {
            var query = (context.searchQuery ?? "").Trim();
            var user = NavigatorUserState.instance;

            var scores = new Dictionary<string, double>();
            foreach (var entry in user.frecency)
                scores[entry.guid] = Navigator.CurrentScore(entry);
            foreach (var entry in user.fileFrecency)
                scores[entry.guid] = Navigator.CurrentScore(entry);
            var pinned = new HashSet<string>(Navigator.AllPins().Select(p => p.pin.guid));

            foreach (var entry in Candidates(pinned, user))
            {
                var note = Navigator.GetNote(entry.guid);
                int match = 0;
                if (query.Length > 0)
                {
                    match = FuzzyMatcher.Score(query, entry.name);
                    int pathMatch = FuzzyMatcher.Score(query, entry.path);
                    if (pathMatch > match) match = pathMatch - 2;
                    if (note.Length > 0)
                    {
                        int noteMatch = FuzzyMatcher.Score(query, note);
                        if (noteMatch > match) match = noteMatch;
                    }
                    if (match < 0) continue;
                }
                else if (!pinned.Contains(entry.guid) && !scores.ContainsKey(entry.guid))
                {
                    continue; // empty query: only pinned and previously visited assets
                }

                bool isPinned = pinned.Contains(entry.guid);
                double rank = match * 10.0 + (isPinned ? 300.0 : 0.0) + (scores.TryGetValue(entry.guid, out var s) ? s * 20.0 : 0.0);
                int score = (int)(100000.0 - rank); // Unity Search sorts ascending: lower score first
                string description = entry.path + (note.Length > 0 ? "   —  " + note : "") + (isPinned ? "   •  pinned" : "");
                items.Add(provider.CreateItem(context, entry.guid, score, entry.name, description, null, entry.path));
            }
            return null;
        }

        /// <summary>All folders (indexed once per asset-database change) plus pinned and recently used files.</summary>
        static IEnumerable<Entry> Candidates(HashSet<string> pinned, NavigatorUserState user)
        {
            foreach (var entry in FolderIndex())
                yield return entry;

            var seen = new HashSet<string>();
            foreach (var guid in pinned.Concat(user.fileFrecency.Select(e => e.guid)))
            {
                if (!seen.Add(guid)) continue;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) continue;
                yield return new Entry { guid = guid, path = path, name = Navigator.NameOf(path), isFolder = false };
            }
        }

        static List<Entry> FolderIndex()
        {
            if (s_FolderIndex != null) return s_FolderIndex;
            bool ignorePackages = NavigatorUserState.instance.ignorePackages;
            var list = new List<Entry>();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (ignorePackages && path.StartsWith("Packages/", StringComparison.Ordinal)) continue;
                if (!AssetDatabase.IsValidFolder(path)) continue;
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) continue;
                list.Add(new Entry { guid = guid, path = path, name = Navigator.NameOf(path), isFolder = true });
            }
            s_FolderIndex = list;
            return list;
        }
    }
}
