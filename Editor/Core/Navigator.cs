// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    internal enum VisitSource
    {
        Selection,
        ActiveFolder,
        Tool
    }

    /// <summary>
    /// Central facade for history, frequency ranking, pins, colors, notes, navigation and persistence.
    /// Everything else (window, toolbar, menus, shortcuts, search) talks to this class.
    /// Folders and files are both plain asset GUIDs here; only back/forward history is folder-only.
    /// </summary>
    internal static class Navigator
    {
        /// <summary>Raised after any change to history, pins, colors or notes. UI refreshes from this.</summary>
        public static event Action Changed;

        static NavigatorUserState User => NavigatorUserState.instance;
        static NavigatorSharedSettings Shared => NavigatorSharedSettings.instance;

        static bool s_UserDirty;
        static bool s_SharedDirty;
        static double s_NextSave;

        [InitializeOnLoadMethod]
        static void Init()
        {
            // Idempotent subscriptions: domain reload on Play is off by default in Unity 6.6, so statics persist.
            EditorApplication.update -= SaveTick;
            EditorApplication.update += SaveTick;
            AssemblyReloadEvents.beforeAssemblyReload -= FlushSaves;
            AssemblyReloadEvents.beforeAssemblyReload += FlushSaves;
            EditorApplication.quitting -= FlushSaves;
            EditorApplication.quitting += FlushSaves;
        }

        // ------------------------------------------------------------------ lookups

        public static AssetInfo GetInfo(string guid)
        {
            var path = string.IsNullOrEmpty(guid) ? "" : AssetDatabase.GUIDToAssetPath(guid);
            bool exists = !string.IsNullOrEmpty(path);
            return new AssetInfo
            {
                guid = guid,
                path = path,
                name = exists ? NameOf(path) : "(missing)",
                isFolder = exists && AssetDatabase.IsValidFolder(path)
            };
        }

        public static string NameOf(string path)
        {
            int i = path.LastIndexOf('/');
            return i < 0 ? path : path.Substring(i + 1);
        }

        public static string ParentOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int i = path.LastIndexOf('/');
            return i < 0 ? "" : path.Substring(0, i);
        }

        public static string GuidOf(string path)
        {
            return string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
        }

        public static bool AssetExists(string guid)
        {
            return !string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid));
        }

        public static bool IsFolderGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return false;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path);
        }

        /// <summary>The folder shown in the Project window (two-column layout) or, failing that, the current history entry.</summary>
        public static string CurrentFolderGuid()
        {
            if (ProjectBrowserBridge.TryGetActiveFolderPath(out var path) && AssetDatabase.IsValidFolder(path))
            {
                var guid = GuidOf(path);
                if (!string.IsNullOrEmpty(guid)) return guid;
            }
            return User.history.Current;
        }

        static bool InPackages(string guid)
        {
            return AssetDatabase.GUIDToAssetPath(guid).StartsWith("Packages/", StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------ recording

        /// <summary>Folder visit. Safe to call often: repeats of the current folder are ignored.</summary>
        public static void RecordVisit(string guid, VisitSource source)
        {
            if (string.IsNullOrEmpty(guid)) return;
            var user = User;
            if (user.ignorePackages && InPackages(guid)) return;
            if (!user.history.Push(guid, user.maxHistory)) return;
            Bump(user.frecency, guid, Mathf.Max(50, user.maxHistory * 2));
            MarkDirty(user: true);
            RaiseChanged();
        }

        /// <summary>File visit (only when "track files" is enabled). Feeds the "Recent files" list.</summary>
        public static void RecordFileVisit(string guid)
        {
            var user = User;
            if (!user.trackFiles || string.IsNullOrEmpty(guid)) return;
            if (user.ignorePackages && InPackages(guid)) return;
            Bump(user.fileFrecency, guid, Mathf.Max(50, user.recentFilesCount * 10));
            MarkDirty(user: true);
            RaiseChanged();
        }

        static void Bump(List<FrecencyEntry> list, string guid, int cap)
        {
            long now = DateTime.UtcNow.Ticks;
            var entry = list.Find(e => e.guid == guid);
            if (entry == null)
            {
                entry = new FrecencyEntry { guid = guid };
                list.Add(entry);
            }
            entry.score = Frecency.Bumped(entry.score, entry.lastVisitTicks, now, User.halfLifeDays);
            entry.lastVisitTicks = now;

            if (list.Count > cap)
            {
                list.Sort((a, b) => CurrentScore(b).CompareTo(CurrentScore(a)));
                list.RemoveRange(cap, list.Count - cap);
            }
        }

        public static double CurrentScore(FrecencyEntry entry)
        {
            return Frecency.Decayed(entry.score, entry.lastVisitTicks, DateTime.UtcNow.Ticks, User.halfLifeDays);
        }

        // ------------------------------------------------------------------ queries

        public static List<AssetInfo> Recent(int max)
        {
            return RecentFrom(User.frecency, max);
        }

        public static List<AssetInfo> RecentFiles(int max)
        {
            return RecentFrom(User.fileFrecency, max);
        }

        static List<AssetInfo> RecentFrom(List<FrecencyEntry> list, int max)
        {
            var result = new List<AssetInfo>();
            foreach (var entry in list.OrderByDescending(e => e.lastVisitTicks))
            {
                var info = GetInfo(entry.guid);
                if (!info.Exists) continue;
                result.Add(info);
                if (result.Count >= max) break;
            }
            return result;
        }

        public static List<(AssetInfo info, double score)> Frequent(int max)
        {
            var result = new List<(AssetInfo info, double score)>();
            foreach (var entry in User.frecency.OrderByDescending(CurrentScore))
            {
                var info = GetInfo(entry.guid);
                if (!info.Exists) continue;
                result.Add((info, CurrentScore(entry)));
                if (result.Count >= max) break;
            }
            return result;
        }

        /// <summary>Team pins first, then personal pins, in their stored order.</summary>
        public static IEnumerable<(AssetPin pin, bool shared)> AllPins()
        {
            return Shared.pins.Select(p => (p, true)).Concat(User.pins.Select(p => (p, false)));
        }

        // ------------------------------------------------------------------ navigation

        public static bool CanGoBack => User.history.CanGoBack;
        public static bool CanGoForward => User.history.CanGoForward;

        public static void Back()
        {
            var history = User.history;
            history.RemoveAll(g => !IsFolderGuid(g));
            var guid = history.Back();
            if (guid == null) return;
            Navigate(guid);
            MarkDirty(user: true);
            RaiseChanged();
        }

        public static void Forward()
        {
            var history = User.history;
            history.RemoveAll(g => !IsFolderGuid(g));
            var guid = history.Forward();
            if (guid == null) return;
            Navigate(guid);
            MarkDirty(user: true);
            RaiseChanged();
        }

        /// <summary>Folders: show in the Project window and record the visit. Files: select and ping.</summary>
        public static void Open(string guid)
        {
            var info = GetInfo(guid);
            if (!info.Exists)
            {
                Debug.LogWarning("[Project Navigator] That asset no longer exists.");
                return;
            }
            if (info.isFolder)
            {
                var user = User;
                if (user.history.Push(guid, user.maxHistory))
                    Bump(user.frecency, guid, Mathf.Max(50, user.maxHistory * 2));
                Navigate(guid);
                MarkDirty(user: true);
                RaiseChanged();
            }
            else
            {
                SelectAndPing(guid); // the tracker records the file visit if tracking is on
            }
        }

        /// <summary>Opens the asset the way a double-click in the Project window would (scene, prefab stage, IDE, external app).</summary>
        public static void OpenAsset(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.IsValidFolder(path))
            {
                Open(guid);
                return;
            }
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj != null) AssetDatabase.OpenAsset(obj);
        }

        static void Navigate(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                ProjectBrowserBridge.ShowFolder(path);
        }

        public static void SelectAndPing(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null) return;
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        public static void GoToParent()
        {
            var current = CurrentFolderGuid();
            if (string.IsNullOrEmpty(current)) return;
            var parent = ParentOf(AssetDatabase.GUIDToAssetPath(current));
            if (string.IsNullOrEmpty(parent)) return;
            var guid = GuidOf(parent);
            if (!string.IsNullOrEmpty(guid)) Open(guid);
        }

        public static void OpenPinSlot(int index)
        {
            var slot = AllPins().Skip(index).FirstOrDefault();
            if (slot.pin != null) Open(slot.pin.guid);
        }

        // ------------------------------------------------------------------ pins (folders and files)

        public static bool IsPinned(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return false;
            return Shared.pins.Any(p => p.guid == guid) || User.pins.Any(p => p.guid == guid);
        }

        public static AssetPin FindPin(string guid, out bool shared)
        {
            var pin = Shared.FindPin(guid);
            shared = pin != null;
            return pin ?? User.pins.Find(p => p.guid == guid);
        }

        public static void Pin(string guid, bool shared)
        {
            if (!AssetExists(guid) || IsPinned(guid)) return;
            (shared ? Shared.pins : User.pins).Add(new AssetPin { guid = guid });
            MarkDirty(user: !shared, shared: shared);
            FolderColorResolver.Invalidate();
            RaiseChanged();
        }

        public static void Unpin(string guid)
        {
            bool shared = Shared.pins.RemoveAll(p => p.guid == guid) > 0;
            bool user = User.pins.RemoveAll(p => p.guid == guid) > 0;
            if (!shared && !user) return;
            MarkDirty(user: user, shared: shared);
            FolderColorResolver.Invalidate();
            RaiseChanged();
        }

        /// <summary>Toggles the pin on the folder currently shown in the Project window.</summary>
        public static void PinCurrentFolder(bool shared)
        {
            var guid = CurrentFolderGuid();
            if (string.IsNullOrEmpty(guid)) return;
            if (IsPinned(guid)) Unpin(guid);
            else Pin(guid, shared);
        }

        /// <summary>Changes only the pin's display text. The asset on disk keeps its name.</summary>
        public static void SetPinLabel(string guid, string label)
        {
            var pin = FindPin(guid, out bool shared);
            if (pin == null) return;
            pin.label = (label ?? "").Trim();
            MarkDirty(user: !shared, shared: shared);
            RaiseChanged();
        }

        public static void SetPinGroup(string guid, string group)
        {
            var pin = FindPin(guid, out bool shared);
            if (pin == null) return;
            pin.group = (group ?? "").Trim();
            MarkDirty(user: !shared, shared: shared);
            RaiseChanged();
        }

        public static void MovePin(string guid, int delta)
        {
            bool shared = Shared.pins.Any(p => p.guid == guid);
            var list = shared ? Shared.pins : User.pins;
            int from = list.FindIndex(p => p.guid == guid);
            if (from < 0) return;
            int to = Mathf.Clamp(from + delta, 0, list.Count - 1);
            if (to == from) return;
            var item = list[from];
            list.RemoveAt(from);
            list.Insert(to, item);
            MarkDirty(user: !shared, shared: shared);
            RaiseChanged();
        }

        // ------------------------------------------------------------------ colors (folders and files)

        /// <summary>Sets an explicit tag ("" = explicitly none) on an asset in the given scope.</summary>
        public static void SetColor(string guid, string tagId, bool shared)
        {
            if (string.IsNullOrEmpty(guid)) return;
            tagId = tagId ?? "";
            var list = shared ? Shared.folderColors : User.folderColors;
            var entry = list.Find(f => f.guid == guid);
            if (entry == null) list.Add(new AssetColor { guid = guid, tagId = tagId });
            else entry.tagId = tagId;

            // A team-level assignment should be visible, so drop a personal override of the same asset.
            bool userTouched = !shared;
            if (shared && User.folderColors.RemoveAll(f => f.guid == guid) > 0) userTouched = true;

            MarkDirty(user: userTouched, shared: shared);
            FolderColorResolver.Invalidate();
            RaiseChanged();
        }

        /// <summary>Removes explicit assignments in both scopes; the asset falls back to rules/inheritance.</summary>
        public static void ClearColor(string guid)
        {
            bool user = User.folderColors.RemoveAll(f => f.guid == guid) > 0;
            bool shared = Shared.folderColors.RemoveAll(f => f.guid == guid) > 0;
            if (!user && !shared) return;
            MarkDirty(user: user, shared: shared);
            FolderColorResolver.Invalidate();
            RaiseChanged();
        }

        public static AssetColor FindExplicitColor(string guid, out bool shared)
        {
            var personal = User.folderColors.Find(f => f.guid == guid);
            if (personal != null)
            {
                shared = false;
                return personal;
            }
            shared = true;
            return Shared.FindColor(guid);
        }

        // ------------------------------------------------------------------ notes

        /// <summary>Note text for an asset, personal note first, then the team note. Empty when none.</summary>
        public static string GetNote(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "";
            var note = User.notes.Find(n => n.guid == guid) ?? Shared.FindNote(guid);
            return note == null ? "" : note.text ?? "";
        }

        public static void SetNote(string guid, string text, bool shared)
        {
            if (string.IsNullOrEmpty(guid)) return;
            text = (text ?? "").Trim();
            if (text.Length == 0)
            {
                ClearNote(guid);
                return;
            }
            var list = shared ? Shared.notes : User.notes;
            var note = list.Find(n => n.guid == guid);
            if (note == null) list.Add(new AssetNote { guid = guid, text = text });
            else note.text = text;

            bool userTouched = !shared;
            if (shared && User.notes.RemoveAll(n => n.guid == guid) > 0) userTouched = true;

            MarkDirty(user: userTouched, shared: shared);
            FolderColorResolver.Invalidate();
            RaiseChanged();
        }

        public static void ClearNote(string guid)
        {
            bool user = User.notes.RemoveAll(n => n.guid == guid) > 0;
            bool shared = Shared.notes.RemoveAll(n => n.guid == guid) > 0;
            if (!user && !shared) return;
            MarkDirty(user: user, shared: shared);
            FolderColorResolver.Invalidate();
            RaiseChanged();
        }

        // ------------------------------------------------------------------ rename on disk

        /// <summary>Renames the asset itself (not the pin label). Returns false with an error message on failure.</summary>
        public static bool RenameAsset(string guid, string newName, out string error)
        {
            error = "";
            var path = AssetDatabase.GUIDToAssetPath(guid);
            newName = (newName ?? "").Trim();
            if (string.IsNullOrEmpty(path) || newName.Length == 0 || newName.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                error = "Invalid name.";
                return false;
            }
            error = AssetDatabase.RenameAsset(path, newName);
            if (!string.IsNullOrEmpty(error)) return false;
            FolderColorResolver.Invalidate();
            RaiseChanged();
            return true;
        }

        // ------------------------------------------------------------------ legacy recovery

        /// <summary>
        /// Merges pins, colors, notes, tags, rules and frequency data from the legacy .asset files into
        /// the current data (nothing existing is removed). Returns a short summary for the user.
        /// </summary>
        public static string RecoverFromLegacyAssets()
        {
            int added = 0;
            var shared = Shared;
            var user = User;

            var sharedTemp = ScriptableObject.CreateInstance<NavigatorSharedSettings>();
            var userTemp = ScriptableObject.CreateInstance<NavigatorUserState>();
            sharedTemp.hideFlags = HideFlags.HideAndDontSave;
            userTemp.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                // Temp instances must not pick up current data: wipe them, then parse the legacy files into them.
                sharedTemp.tags.Clear(); sharedTemp.folderColors.Clear(); sharedTemp.rules.Clear(); sharedTemp.pins.Clear(); sharedTemp.notes.Clear();
                userTemp.pins.Clear(); userTemp.folderColors.Clear(); userTemp.notes.Clear(); userTemp.frecency.Clear(); userTemp.fileFrecency.Clear(); userTemp.history.Clear();

                bool anyShared = LegacyAssetImporter.TryImport(sharedTemp, NavigatorSharedSettings.LegacyAssetPath, out var sharedError);
                bool anyUser = LegacyAssetImporter.TryImport(userTemp, NavigatorUserState.LegacyAssetPath, out var userError);
                if (!anyShared && !anyUser)
                    return "No legacy .asset files found" + (sharedError != null || userError != null ? " (" + (sharedError ?? userError) + ")" : "") + ".";

                if (anyShared)
                {
                    foreach (var tag in sharedTemp.tags) if (shared.FindTag(tag.id) == null) { shared.tags.Add(tag); added++; }
                    foreach (var rule in sharedTemp.rules) if (!shared.rules.Exists(r => r.pattern == rule.pattern && r.tagId == rule.tagId)) { shared.rules.Add(rule); added++; }
                    added += MergePins(shared.pins, sharedTemp.pins);
                    added += MergeColors(shared.folderColors, sharedTemp.folderColors);
                    added += MergeNotes(shared.notes, sharedTemp.notes);
                }
                if (anyUser)
                {
                    added += MergePins(user.pins, userTemp.pins);
                    added += MergeColors(user.folderColors, userTemp.folderColors);
                    added += MergeNotes(user.notes, userTemp.notes);
                    foreach (var entry in userTemp.frecency.Concat(userTemp.fileFrecency))
                    {
                        var list = IsFolderGuid(entry.guid) ? user.frecency : user.fileFrecency;
                        var existing = list.Find(e => e.guid == entry.guid);
                        if (existing == null) { list.Add(entry); added++; }
                        else if (entry.lastVisitTicks > existing.lastVisitTicks) { existing.lastVisitTicks = entry.lastVisitTicks; existing.score = Math.Max(existing.score, entry.score); }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sharedTemp);
                UnityEngine.Object.DestroyImmediate(userTemp);
            }

            MarkDirty(user: true, shared: true);
            FolderColorResolver.Invalidate();
            RaiseChanged();
            return added == 0 ? "Legacy files read; nothing new to add." : $"Recovered {added} entries from the legacy .asset files.";
        }

        static int MergePins(List<AssetPin> into, List<AssetPin> from)
        {
            int added = 0;
            foreach (var pin in from)
                if (AssetExists(pin.guid) && !IsPinned(pin.guid) && !into.Exists(p => p.guid == pin.guid)) { into.Add(pin); added++; }
            return added;
        }

        static int MergeColors(List<AssetColor> into, List<AssetColor> from)
        {
            int added = 0;
            foreach (var color in from)
                if (AssetExists(color.guid) && !into.Exists(c => c.guid == color.guid)) { into.Add(color); added++; }
            return added;
        }

        static int MergeNotes(List<AssetNote> into, List<AssetNote> from)
        {
            int added = 0;
            foreach (var note in from)
                if (AssetExists(note.guid) && !string.IsNullOrEmpty(note.text) && !into.Exists(n => n.guid == note.guid)) { into.Add(note); added++; }
            return added;
        }

        // ------------------------------------------------------------------ maintenance

        public static void Forget(string guid)
        {
            User.history.RemoveAll(g => g == guid);
            User.frecency.RemoveAll(e => e.guid == guid);
            User.fileFrecency.RemoveAll(e => e.guid == guid);
            MarkDirty(user: true);
            RaiseChanged();
        }

        public static void ClearHistory()
        {
            User.history.Clear();
            User.frecency.Clear();
            User.fileFrecency.Clear();
            MarkDirty(user: true);
            RaiseChanged();
        }

        /// <summary>Drops personal entries whose asset no longer exists. Team data is never pruned automatically.</summary>
        public static void PruneMissing()
        {
            var user = User;
            int removed = 0;
            removed += user.history.RemoveAll(g => !IsFolderGuid(g));
            removed += user.frecency.RemoveAll(e => !IsFolderGuid(e.guid));
            removed += user.fileFrecency.RemoveAll(e => !AssetExists(e.guid));
            removed += user.pins.RemoveAll(p => !AssetExists(p.guid));
            removed += user.folderColors.RemoveAll(f => !AssetExists(f.guid));
            removed += user.notes.RemoveAll(n => !AssetExists(n.guid));
            if (removed == 0) return;
            MarkDirty(user: true);
            FolderColorResolver.Invalidate();
            RaiseChanged();
        }

        public static int PruneMissingShared()
        {
            var shared = Shared;
            int removed = shared.pins.RemoveAll(p => !AssetExists(p.guid));
            removed += shared.folderColors.RemoveAll(f => !AssetExists(f.guid));
            removed += shared.notes.RemoveAll(n => !AssetExists(n.guid));
            if (removed > 0)
            {
                MarkDirty(shared: true);
                FolderColorResolver.Invalidate();
                RaiseChanged();
            }
            return removed;
        }

        public static void NotifyChanged()
        {
            RaiseChanged();
        }

        static void RaiseChanged()
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ------------------------------------------------------------------ persistence

        /// <summary>Saves are batched: at most one disk write per scope every couple of seconds.</summary>
        public static void MarkDirty(bool user = false, bool shared = false)
        {
            if (user) s_UserDirty = true;
            if (shared) s_SharedDirty = true;
            s_NextSave = EditorApplication.timeSinceStartup + 2.0;
        }

        static void SaveTick()
        {
            if (!s_UserDirty && !s_SharedDirty) return;
            if (EditorApplication.timeSinceStartup < s_NextSave) return;
            FlushSaves();
        }

        public static void FlushSaves()
        {
            if (s_UserDirty)
            {
                s_UserDirty = false;
                User.SaveNow();
            }
            if (s_SharedDirty)
            {
                s_SharedDirty = false;
                Shared.SaveNow();
            }
        }
    }
}
