// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Browser-style back/forward stack of folder GUIDs. Pure C# and serializable, so it survives
    /// domain reloads inside <see cref="NavigatorUserState"/>. Covered by edit-mode tests.
    /// </summary>
    [Serializable]
    internal sealed class HistoryStack
    {
        [SerializeField] List<string> entries = new List<string>();
        [SerializeField] int cursor = -1;

        public int Count => entries.Count;
        public int Cursor => cursor;
        public IReadOnlyList<string> Entries => entries;
        public string Current => cursor >= 0 && cursor < entries.Count ? entries[cursor] : null;
        public bool CanGoBack => cursor > 0;
        public bool CanGoForward => cursor >= 0 && cursor < entries.Count - 1;

        /// <summary>Records a visit. Returns false when nothing changed (same entry as the current one).</summary>
        public bool Push(string guid, int maxEntries)
        {
            if (string.IsNullOrEmpty(guid)) return false;
            if (Current == guid) return false;

            // Visiting something new while "in the past" drops the forward entries, like a browser does.
            if (cursor < entries.Count - 1)
                entries.RemoveRange(cursor + 1, entries.Count - cursor - 1);

            entries.Add(guid);
            cursor = entries.Count - 1;
            Trim(maxEntries);
            return true;
        }

        public string Back()
        {
            if (!CanGoBack) return null;
            cursor--;
            return entries[cursor];
        }

        public string Forward()
        {
            if (!CanGoForward) return null;
            cursor++;
            return entries[cursor];
        }

        public void Trim(int maxEntries)
        {
            if (maxEntries < 1) maxEntries = 1;
            int excess = entries.Count - maxEntries;
            if (excess <= 0) return;
            entries.RemoveRange(0, excess);
            cursor = Mathf.Max(0, cursor - excess);
        }

        /// <summary>Removes matching entries, keeping the cursor on the same logical entry when possible.</summary>
        public int RemoveAll(Predicate<string> match)
        {
            string current = Current;
            int removed = entries.RemoveAll(match);
            if (removed == 0) return 0;

            cursor = current != null ? entries.LastIndexOf(current) : -1;
            if (cursor < 0) cursor = entries.Count - 1;

            // Removing entries can leave the same guid twice in a row; collapse those.
            for (int i = entries.Count - 1; i > 0; i--)
            {
                if (entries[i] != entries[i - 1]) continue;
                entries.RemoveAt(i);
                if (cursor >= i) cursor--;
            }
            return removed;
        }

        public void Clear()
        {
            entries.Clear();
            cursor = -1;
        }
    }
}
