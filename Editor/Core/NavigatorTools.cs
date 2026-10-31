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
    /// Editor tools and windows: pins to menu items and window types, the live list of open windows,
    /// per-window labels and colors, recently focused windows, and menu-item search.
    /// Tool pins are global (per user, all projects) unless pinned for the team, which stores them
    /// in the project's shared settings.
    /// </summary>
    internal static class NavigatorTools
    {
        public static readonly (string name, string hex)[] PresetColors =
        {
            ("Yellow", "#E8C547"), ("Green", "#5DBB63"), ("Blue", "#4A90E2"), ("Orange", "#F0883A"),
            ("Red", "#D9534F"), ("Purple", "#9B6BD6"), ("Teal", "#2BB3A3"), ("Grey", "#9A9A9A")
        };

        public const int MaxRecentWindows = 30;

        static NavigatorGlobalData Global => NavigatorGlobal.Data;
        static NavigatorSharedSettings Shared => NavigatorSharedSettings.instance;

        public static Color PresetColor(int index)
        {
            ColorUtility.TryParseHtmlString(PresetColors[Mathf.Clamp(index, 0, PresetColors.Length - 1)].hex, out var c);
            return c;
        }

        // ------------------------------------------------------------------ pins

        /// <summary>Team pins first, then personal (global) pins.</summary>
        public static IEnumerable<(ToolPin pin, bool shared)> AllToolPins()
        {
            return Shared.toolPins.Select(p => (p, true)).Concat(Global.toolPins.Select(p => (p, false)));
        }

        public static ToolPin FindToolPin(string id, out bool shared)
        {
            var pin = Shared.toolPins.Find(p => p.id == id);
            shared = pin != null;
            return pin ?? Global.toolPins.Find(p => p.id == id);
        }

        public static bool IsMenuPinned(string menuPath)
        {
            return AllToolPins().Any(t => t.pin.kind == ToolPinKind.MenuItem && t.pin.menuPath == menuPath);
        }

        public static bool IsWindowTypePinned(Type type)
        {
            var key = TypeKey(type);
            return AllToolPins().Any(t => t.pin.kind == ToolPinKind.Window && t.pin.windowType == key);
        }

        public static ToolPin PinMenuItem(string menuPath, string label, bool shared)
        {
            menuPath = (menuPath ?? "").Trim().TrimStart('/');
            if (menuPath.Length == 0) return null;
            var existing = AllToolPins().FirstOrDefault(t => t.pin.kind == ToolPinKind.MenuItem && t.pin.menuPath == menuPath).pin;
            if (existing != null) return existing;
            var pin = new ToolPin { kind = ToolPinKind.MenuItem, menuPath = menuPath, label = (label ?? "").Trim() };
            Add(pin, shared);
            return pin;
        }

        public static ToolPin PinWindowType(Type type, bool shared)
        {
            if (type == null) return null;
            var key = TypeKey(type);
            var existing = AllToolPins().FirstOrDefault(t => t.pin.kind == ToolPinKind.Window && t.pin.windowType == key).pin;
            if (existing != null) return existing;
            var pin = new ToolPin { kind = ToolPinKind.Window, windowType = key };
            Add(pin, shared);
            return pin;
        }

        static void Add(ToolPin pin, bool shared)
        {
            if (shared)
            {
                Shared.toolPins.Add(pin);
                Navigator.MarkDirty(shared: true);
            }
            else
            {
                Global.toolPins.Add(pin);
                NavigatorGlobal.MarkDirty();
            }
            Navigator.NotifyChanged();
        }

        public static void UnpinTool(string id)
        {
            bool shared = Shared.toolPins.RemoveAll(p => p.id == id) > 0;
            bool global = Global.toolPins.RemoveAll(p => p.id == id) > 0;
            if (!shared && !global) return;
            Navigator.MarkDirty(shared: shared, global: global);
            Navigator.NotifyChanged();
        }

        public static void SetToolLabel(string id, string label)
        {
            var pin = FindToolPin(id, out bool shared);
            if (pin == null) return;
            pin.label = (label ?? "").Trim();
            Navigator.MarkDirty(shared: shared, global: !shared);
            Navigator.NotifyChanged();
        }

        public static void SetToolGroup(string id, string group)
        {
            var pin = FindToolPin(id, out bool shared);
            if (pin == null) return;
            pin.group = (group ?? "").Trim();
            Navigator.MarkDirty(shared: shared, global: !shared);
            Navigator.NotifyChanged();
        }

        public static void SetToolColor(string id, Color? color)
        {
            var pin = FindToolPin(id, out bool shared);
            if (pin == null) return;
            pin.hasColor = color.HasValue;
            pin.color = color ?? Color.white;
            Navigator.MarkDirty(shared: shared, global: !shared);
            ToolTracker.ApplyMarksNow();
            Navigator.NotifyChanged();
        }

        public static void MoveToolPin(string id, int delta)
        {
            var pin = FindToolPin(id, out bool shared);
            if (pin == null) return;
            var list = shared ? Shared.toolPins : Global.toolPins;
            int from = list.IndexOf(pin);
            int to = Mathf.Clamp(from + delta, 0, list.Count - 1);
            if (from < 0 || to == from) return;
            list.RemoveAt(from);
            list.Insert(to, pin);
            Navigator.MarkDirty(shared: shared, global: !shared);
            Navigator.NotifyChanged();
        }

        public static string DefaultLabel(ToolPin pin)
        {
            if (pin.kind == ToolPinKind.MenuItem)
            {
                int i = pin.menuPath.LastIndexOf('/');
                return i < 0 ? pin.menuPath : pin.menuPath.Substring(i + 1);
            }
            var type = ResolveType(pin.windowType);
            return type != null ? NiceTypeName(type) : ShortTypeName(pin.windowType);
        }

        public static string Label(ToolPin pin)
        {
            return string.IsNullOrEmpty(pin.label) ? DefaultLabel(pin) : pin.label;
        }

        public static string Detail(ToolPin pin)
        {
            return pin.kind == ToolPinKind.MenuItem ? pin.menuPath : "window · " + ShortTypeName(pin.windowType);
        }

        public static Texture Icon(ToolPin pin)
        {
            if (pin.kind == ToolPinKind.Window)
            {
                var type = ResolveType(pin.windowType);
                if (type != null)
                {
                    var open = OpenWindows().FirstOrDefault(w => w.GetType() == type);
                    if (open != null)
                    {
                        var icon = ToolTracker.OriginalIcon(open);
                        if (icon != null) return icon;
                    }
                }
                return EditorGUIUtility.FindTexture("_Popup");
            }
            return EditorGUIUtility.FindTexture("PlayButton");
        }

        public static void Launch(ToolPin pin)
        {
            if (pin == null) return;
            if (pin.kind == ToolPinKind.MenuItem)
            {
                if (!EditorApplication.ExecuteMenuItem(pin.menuPath))
                    Debug.LogWarning($"[Project Navigator] Menu item not found or disabled: {pin.menuPath}");
                return;
            }
            var type = ResolveType(pin.windowType);
            if (type == null)
            {
                Debug.LogWarning($"[Project Navigator] Window type not found (is the tool still installed?): {pin.windowType}");
                return;
            }
            FocusOrOpen(type);
        }

        // ------------------------------------------------------------------ windows

        public static bool IsNavigatorWindow(EditorWindow window)
        {
            return window != null && window.GetType().Namespace == typeof(NavigatorTools).Namespace;
        }

        /// <summary>Open editor windows (docked, floating, and inactive tabs), excluding the Navigator's own.</summary>
        public static List<EditorWindow> OpenWindows()
        {
            var list = new List<EditorWindow>();
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window == null || IsNavigatorWindow(window)) continue;
                if (window.titleContent == null || string.IsNullOrEmpty(window.titleContent.text)) continue;
                var rect = window.position;
                if (rect.width <= 1f || rect.height <= 1f) continue;
                list.Add(window);
            }
            return list.OrderBy(w => w.titleContent.text, StringComparer.OrdinalIgnoreCase).ThenBy(w => w.GetHashCode()).ToList();
        }

        public static string TypeKey(Type type)
        {
            return type.AssemblyQualifiedName;
        }

        public static string ShortTypeName(string typeKey)
        {
            if (string.IsNullOrEmpty(typeKey)) return "";
            int comma = typeKey.IndexOf(',');
            var full = comma < 0 ? typeKey : typeKey.Substring(0, comma);
            int dot = full.LastIndexOf('.');
            return dot < 0 ? full : full.Substring(dot + 1);
        }

        public static string NiceTypeName(Type type)
        {
            var name = ObjectNames.NicifyVariableName(type.Name);
            return name.EndsWith(" Window", StringComparison.Ordinal) ? name.Substring(0, name.Length - 7) : name;
        }

        public static Type ResolveType(string typeKey)
        {
            if (string.IsNullOrEmpty(typeKey)) return null;
            var type = Type.GetType(typeKey);
            if (type != null) return type;
            // The assembly name may have changed (package update, Unity upgrade): match by full name among window types.
            int comma = typeKey.IndexOf(',');
            var full = comma < 0 ? typeKey : typeKey.Substring(0, comma);
            foreach (var candidate in TypeCache.GetTypesDerivedFrom<EditorWindow>())
                if (candidate.FullName == full) return candidate;
            return null;
        }

        /// <summary>Focuses a window of this type; with several open, cycles to the next one; with none, opens it.</summary>
        public static void FocusOrOpen(Type type)
        {
            var open = OpenWindows().Where(w => w.GetType() == type).ToList();
            if (open.Count == 0)
            {
                var window = EditorWindow.GetWindow(type);
                if (window == null) return;
                window.Show();
                window.Focus();
                return;
            }
            int index = open.IndexOf(EditorWindow.focusedWindow);
            var next = open[(index + 1) % open.Count];
            next.Focus();
        }

        public static void Focus(EditorWindow window)
        {
            if (window == null) return;
            window.Show();
            window.Focus();
        }

        // ------------------------------------------------------------------ window marks (labels and colors)

        /// <summary>"Type|Title|ordinal": stable within a session, best-effort across editor restarts.</summary>
        public static string InstanceKey(EditorWindow window)
        {
            return InstanceKey(window, OpenWindows());
        }

        /// <summary>Same, with the open-window list supplied by the caller (avoids re-listing windows per call).</summary>
        public static string InstanceKey(EditorWindow window, IReadOnlyList<EditorWindow> openWindows)
        {
            var title = window.titleContent.text;
            var type = window.GetType();
            int ordinal = 0;
            for (int i = 0; i < openWindows.Count; i++)
            {
                var other = openWindows[i];
                if (other == window) break;
                if (other.GetType() == type && other.titleContent.text == title) ordinal++;
            }
            return type.FullName + "|" + title + "|" + ordinal;
        }

        public static WindowMark FindMark(EditorWindow window)
        {
            return window == null ? null : FindMark(window, OpenWindows());
        }

        public static WindowMark FindMark(EditorWindow window, IReadOnlyList<EditorWindow> openWindows)
        {
            if (window == null) return null;
            var marks = Global.windowMarks;
            if (marks.Count == 0) return null;
            var key = InstanceKey(window, openWindows);
            var typeKey = window.GetType().FullName;
            return marks.Find(m => !m.byType && m.key == key)
                ?? marks.Find(m => m.byType && m.key == typeKey);
        }

        /// <summary>Color pin of a window-type tool pin, if any.</summary>
        public static bool TryGetToolColorForType(Type type, out Color color)
        {
            color = Color.white;
            if (type == null) return false;
            var key = TypeKey(type);
            foreach (var (pin, _) in AllToolPins())
            {
                if (pin.kind != ToolPinKind.Window || !pin.hasColor || pin.windowType != key) continue;
                color = pin.color;
                return true;
            }
            return false;
        }

        public static bool AnyColoredWindowPins()
        {
            return AllToolPins().Any(t => t.pin.kind == ToolPinKind.Window && t.pin.hasColor);
        }

        /// <summary>The color a window is shown with: its own mark, else (when enabled) the color of the tool pin that opens it.</summary>
        public static bool TryGetEffectiveColor(EditorWindow window, IReadOnlyList<EditorWindow> openWindows, out Color color)
        {
            var mark = FindMark(window, openWindows);
            if (mark != null && mark.hasColor)
            {
                color = mark.color;
                return true;
            }
            if (Global.toolColorsOnWindows && TryGetToolColorForType(window.GetType(), out color))
                return true;
            color = Color.white;
            return false;
        }

        /// <summary>Cheap fingerprint of the open-window state, for refreshing lists only when something changed.</summary>
        public static string WindowsSignature(IReadOnlyList<EditorWindow> openWindows)
        {
            var sb = new System.Text.StringBuilder(openWindows.Count * 24);
            for (int i = 0; i < openWindows.Count; i++)
            {
                var w = openWindows[i];
                sb.Append(w.GetHashCode()).Append(w.hasFocus ? 'F' : '-').Append(w.docked ? 'D' : '-').Append(w.titleContent.text).Append('|');
            }
            return sb.ToString();
        }

        public static string DisplayName(EditorWindow window)
        {
            var mark = FindMark(window);
            return mark != null && !string.IsNullOrEmpty(mark.label) ? mark.label : window.titleContent.text;
        }

        static WindowMark GetOrCreateMark(EditorWindow window, bool byType)
        {
            var key = byType ? window.GetType().FullName : InstanceKey(window);
            var mark = Global.windowMarks.Find(m => m.byType == byType && m.key == key);
            if (mark == null)
            {
                mark = new WindowMark { key = key, byType = byType };
                Global.windowMarks.Add(mark);
            }
            return mark;
        }

        public static void SetWindowColor(EditorWindow window, bool byType, Color? color)
        {
            if (window == null) return;
            if (!byType)
            {
                // An instance color overrides a type color; clearing the instance color also clears it.
                var instance = Global.windowMarks.Find(m => !m.byType && m.key == InstanceKey(window));
                if (!color.HasValue && instance == null)
                {
                    var typeMark = Global.windowMarks.Find(m => m.byType && m.key == window.GetType().FullName);
                    if (typeMark != null) { typeMark.hasColor = false; Prune(typeMark); }
                    Commit();
                    return;
                }
            }
            var mark = GetOrCreateMark(window, byType);
            mark.hasColor = color.HasValue;
            mark.color = color ?? Color.white;
            Prune(mark);
            Commit();
        }

        public static void SetWindowLabel(EditorWindow window, string label)
        {
            if (window == null) return;
            var mark = GetOrCreateMark(window, false);
            mark.label = (label ?? "").Trim();
            Prune(mark);
            Commit();
        }

        static void Prune(WindowMark mark)
        {
            if (!mark.hasColor && string.IsNullOrEmpty(mark.label))
                Global.windowMarks.Remove(mark);
        }

        static void Commit()
        {
            NavigatorGlobal.MarkDirty();
            ToolTracker.ApplyMarksNow();
            Navigator.NotifyChanged();
        }

        // ------------------------------------------------------------------ recent windows

        public static void RecordRecent(EditorWindow window)
        {
            if (window == null) return;
            var data = Global;
            var key = TypeKey(window.GetType());
            var title = window.titleContent.text;
            var entry = data.recentWindows.Find(r => r.windowType == key && r.title == title);
            if (entry == null)
            {
                entry = new RecentWindow { windowType = key, title = title };
                data.recentWindows.Add(entry);
            }
            entry.lastFocusTicks = DateTime.UtcNow.Ticks;
            if (data.recentWindows.Count > MaxRecentWindows)
            {
                data.recentWindows.Sort((a, b) => b.lastFocusTicks.CompareTo(a.lastFocusTicks));
                data.recentWindows.RemoveRange(MaxRecentWindows, data.recentWindows.Count - MaxRecentWindows);
            }
            NavigatorGlobal.MarkDirty();
            Navigator.NotifyChanged();
        }

        /// <summary>Recently focused windows, newest first; with <paramref name="closedOnly"/> only those with no open instance.</summary>
        public static List<RecentWindow> RecentWindows(int max, bool closedOnly)
        {
            var open = closedOnly ? OpenWindows() : null;
            var result = new List<RecentWindow>();
            foreach (var entry in Global.recentWindows.OrderByDescending(r => r.lastFocusTicks))
            {
                if (closedOnly)
                {
                    var type = ResolveType(entry.windowType);
                    if (type != null && open.Any(w => w.GetType() == type)) continue;
                }
                result.Add(entry);
                if (result.Count >= max) break;
            }
            return result;
        }

        public static void ForgetRecent(RecentWindow entry)
        {
            if (Global.recentWindows.Remove(entry))
            {
                NavigatorGlobal.MarkDirty();
                Navigator.NotifyChanged();
            }
        }

        // ------------------------------------------------------------------ menu search

        /// <summary>Searches every menu item in the editor through Unity Search's menu provider ("m:").</summary>
        public static List<(string path, string label)> SearchMenuItems(string query, int max)
        {
            var result = new List<(string path, string label)>();
            query = (query ?? "").Trim();
            if (query.Length < 2) return result;
            try
            {
                using (var results = SearchService.Request("m:" + query, SearchFlags.Synchronous))
                {
                    foreach (var item in results)
                    {
                        if (item == null) continue;
                        var label = item.GetLabel(results.context, true) ?? "";
                        var description = item.GetDescription(results.context, true) ?? "";
                        var path = !string.IsNullOrEmpty(item.id) && item.id.Contains("/") ? item.id : description;
                        if (string.IsNullOrEmpty(path) || result.Any(r => r.path == path)) continue;
                        result.Add((path, label));
                        if (result.Count >= max) break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Project Navigator] Menu search failed: {e.Message}");
            }
            return result;
        }
    }
}
