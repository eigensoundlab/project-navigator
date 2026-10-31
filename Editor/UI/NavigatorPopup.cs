// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Popup at the cursor, summoned by a shortcut and closed on focus loss, so window stacking never
    /// hides it. Two modes:
    ///  - Navigator (Alt+Shift+Space): pinned, frequent and recent folders/files, pinned tools.
    ///  - Windows (Alt+Shift+W): open editor windows to switch to, then recently used (closed) ones.
    /// Type to filter, ↑/↓ to move, Enter to open, Ctrl+P to pin/unpin, Esc to close.
    /// </summary>
    internal sealed class NavigatorPopup : EditorWindow
    {
        public enum Mode
        {
            Navigator,
            Windows
        }

        enum EntryKind
        {
            Asset,
            Tool,
            Window,
            RecentWindow
        }

        sealed class Entry
        {
            public EntryKind kind;
            public string label;
            public string detail;
            public string note;
            public string section;
            public bool pinned;
            public bool hasColor;
            public Color color;
            public Texture icon;
            // asset
            public string guid;
            public string path;
            public bool isFolder;
            // tool / window
            public ToolPin tool;
            public EditorWindow window;
            public RecentWindow recent;
        }

        const float k_Width = 400f;
        const float k_RowHeight = 20f;
        const int k_MaxVisibleRows = 18;
        const string k_SearchControl = "pn-popup-search";

        static NavigatorPopup s_Instance;

        readonly List<Entry> m_All = new List<Entry>();
        readonly List<Entry> m_Visible = new List<Entry>();
        Mode m_Mode;
        string m_Filter = "";
        int m_Index;
        Vector2 m_Scroll;
        bool m_FocusSearch = true;
        GUIStyle m_RowStyle;
        GUIStyle m_PathStyle;
        GUIStyle m_SectionStyle;

        public static void Toggle(Mode mode)
        {
            if (s_Instance != null)
            {
                bool same = s_Instance.m_Mode == mode;
                s_Instance.Close();
                if (same) return;
            }
            Open(mode);
        }

        public static void Open(Mode mode)
        {
            if (s_Instance != null)
            {
                if (s_Instance.m_Mode == mode)
                {
                    s_Instance.Focus();
                    return;
                }
                s_Instance.Close();
            }

            var window = CreateInstance<NavigatorPopup>();
            window.m_Mode = mode;
            window.Collect();

            float height = Mathf.Clamp(window.m_Visible.Count + 2, 4, k_MaxVisibleRows) * k_RowHeight + 34f;
            var anchor = AnchorAtCursor();

            // ShowAsDropDown keeps the window on screen and closes it when it loses focus.
            window.ShowAsDropDown(new Rect(anchor.x - 20f, anchor.y - 10f, 1f, 1f), new Vector2(k_Width, height));
            window.Focus();
            s_Instance = window;
        }

        /// <summary>Mouse position in screen space, with fallbacks for contexts without a GUI clip (shortcuts, menus).</summary>
        static Vector2 AnchorAtCursor()
        {
            var evt = Event.current;
            if (evt != null)
            {
                try
                {
                    return GUIUtility.GUIToScreenPoint(evt.mousePosition);
                }
                catch (Exception)
                {
                    // Not inside an OnGUI pass; fall through.
                }
                var focused = EditorWindow.focusedWindow;
                if (focused != null)
                    return focused.position.position + evt.mousePosition;
            }
            var main = EditorGUIUtility.GetMainWindowPosition();
            return new Vector2(main.x + main.width * 0.5f - k_Width * 0.5f, main.y + 120f);
        }

        void OnEnable()
        {
            s_Instance = this;
        }

        void OnDisable()
        {
            if (s_Instance == this) s_Instance = null;
        }

        void OnLostFocus()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null) Close();
            };
        }

        // ------------------------------------------------------------------ data

        void Collect()
        {
            m_All.Clear();
            if (m_Mode == Mode.Windows) CollectWindows();
            else CollectNavigator();
            ApplyFilter();
        }

        void CollectNavigator()
        {
            var user = NavigatorUserState.instance;
            var seen = new HashSet<string>();

            foreach (var (pin, shared) in Navigator.AllPins())
            {
                var info = Navigator.GetInfo(pin.guid);
                if (!info.Exists || !seen.Add(info.guid)) continue;
                var entry = MakeAsset(info, "Pinned" + (shared ? " (team)" : ""));
                if (!string.IsNullOrEmpty(pin.label)) entry.label = pin.label;
                m_All.Add(entry);
            }
            foreach (var (pin, shared) in NavigatorTools.AllToolPins())
                m_All.Add(MakeTool(pin, "Tools" + (shared ? " (team)" : "")));
            foreach (var (info, _) in Navigator.Frequent(user.frequentCount))
            {
                if (!seen.Add(info.guid)) continue;
                m_All.Add(MakeAsset(info, "Frequent"));
            }
            foreach (var info in Navigator.Recent(user.recentCount))
            {
                if (!seen.Add(info.guid)) continue;
                m_All.Add(MakeAsset(info, "Recent"));
            }
            if (user.trackFiles)
            {
                foreach (var info in Navigator.RecentFiles(user.recentFilesCount))
                {
                    if (!seen.Add(info.guid)) continue;
                    m_All.Add(MakeAsset(info, "Recent files"));
                }
            }
        }

        void CollectWindows()
        {
            var open = NavigatorTools.OpenWindows();
            foreach (var window in open)
                m_All.Add(MakeWindow(window, open));
            foreach (var recent in NavigatorTools.RecentWindows(NavigatorGlobal.Data.recentWindowsCount, closedOnly: true))
            {
                m_All.Add(new Entry
                {
                    kind = EntryKind.RecentWindow,
                    recent = recent,
                    label = recent.title,
                    detail = NavigatorTools.ShortTypeName(recent.windowType) + " · closed",
                    section = "Recently used",
                    icon = EditorGUIUtility.FindTexture("_Popup")
                });
            }
        }

        static Entry MakeAsset(AssetInfo info, string section)
        {
            var entry = new Entry
            {
                kind = EntryKind.Asset,
                guid = info.guid,
                path = info.path,
                label = info.name,
                detail = Navigator.ParentOf(info.path),
                note = Navigator.GetNote(info.guid),
                section = section,
                isFolder = info.isFolder,
                pinned = Navigator.IsPinned(info.guid),
                icon = AssetDatabase.GetCachedIcon(info.path)
            };
            if (FolderColorResolver.TryGet(info.guid, out var style) && style.hasColor)
            {
                entry.hasColor = true;
                entry.color = style.color;
            }
            return entry;
        }

        static Entry MakeTool(ToolPin pin, string section)
        {
            return new Entry
            {
                kind = EntryKind.Tool,
                tool = pin,
                label = NavigatorTools.Label(pin),
                detail = NavigatorTools.Detail(pin),
                note = pin.group,
                section = section,
                pinned = true,
                hasColor = pin.hasColor,
                color = pin.color,
                icon = NavigatorTools.Icon(pin)
            };
        }

        static Entry MakeWindow(EditorWindow window, IReadOnlyList<EditorWindow> open)
        {
            var mark = NavigatorTools.FindMark(window, open);
            var type = window.GetType();
            return new Entry
            {
                kind = EntryKind.Window,
                window = window,
                label = NavigatorTools.DisplayName(window),
                detail = type.Name + (window.docked ? " · docked" : " · floating") + (window.hasFocus ? " · focused" : ""),
                note = mark != null && !string.IsNullOrEmpty(mark.label) ? window.titleContent.text : "",
                section = "Open windows",
                pinned = NavigatorTools.IsWindowTypePinned(type),
                hasColor = mark != null && mark.hasColor,
                color = mark != null ? mark.color : Color.white,
                icon = ToolTracker.OriginalIcon(window)
            };
        }

        void ApplyFilter()
        {
            m_Visible.Clear();
            if (string.IsNullOrEmpty(m_Filter))
            {
                m_Visible.AddRange(m_All);
            }
            else
            {
                var scored = new List<(Entry entry, int score)>();
                foreach (var entry in m_All)
                {
                    int score = FuzzyMatcher.Score(m_Filter, entry.label);
                    int detailScore = FuzzyMatcher.Score(m_Filter, entry.detail ?? "");
                    if (detailScore - 2 > score) score = detailScore - 2;
                    if (!string.IsNullOrEmpty(entry.note))
                    {
                        int noteScore = FuzzyMatcher.Score(m_Filter, entry.note);
                        if (noteScore > score) score = noteScore;
                    }
                    if (score >= 0) scored.Add((entry, score + (entry.pinned ? 5 : 0)));
                }
                m_Visible.AddRange(scored.OrderByDescending(s => s.score).Select(s => s.entry));
            }
            m_Index = Mathf.Clamp(m_Index, 0, Mathf.Max(0, m_Visible.Count - 1));
        }

        // ------------------------------------------------------------------ GUI

        void OnGUI()
        {
            EnsureStyles();
            HandleKeys();

            var border = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(border, EditorGUIUtility.isProSkin ? new Color(0.14f, 0.14f, 0.14f) : new Color(0.55f, 0.55f, 0.55f));
            EditorGUI.DrawRect(new Rect(1f, 1f, position.width - 2f, position.height - 2f),
                EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.8f, 0.8f, 0.8f));

            GUILayout.BeginArea(new Rect(4f, 4f, position.width - 8f, position.height - 8f));

            GUI.SetNextControlName(k_SearchControl);
            EditorGUI.BeginChangeCheck();
            m_Filter = EditorGUILayout.TextField(m_Filter, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                m_Index = 0;
                ApplyFilter();
            }
            if (m_FocusSearch)
            {
                m_FocusSearch = false;
                EditorGUI.FocusTextInControl(k_SearchControl);
            }

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            if (m_Visible.Count == 0)
            {
                string empty = m_All.Count > 0 ? "No match." : m_Mode == Mode.Windows ? "No open windows." : "Nothing pinned or visited yet.";
                GUILayout.Label(empty, EditorStyles.centeredGreyMiniLabel);
            }
            string lastSection = null;
            for (int i = 0; i < m_Visible.Count; i++)
            {
                var entry = m_Visible[i];
                if (string.IsNullOrEmpty(m_Filter) && entry.section != lastSection)
                {
                    lastSection = entry.section;
                    GUILayout.Label(entry.section, m_SectionStyle);
                }
                DrawRow(entry, i == m_Index);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawRow(Entry entry, bool selected)
        {
            var rect = GUILayoutUtility.GetRect(10f, k_RowHeight, GUILayout.ExpandWidth(true));
            bool hover = rect.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
            {
                if (selected) EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.24f, 0.37f, 0.58f) : new Color(0.24f, 0.49f, 0.9f));
                else if (hover) EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.06f));

                if (entry.hasColor)
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3f, 3f, rect.height - 6f), entry.color);

                var iconRect = new Rect(rect.x + 7f, rect.y + 2f, 16f, 16f);
                if (entry.icon != null)
                {
                    var previous = GUI.color;
                    if (entry.hasColor && entry.isFolder) GUI.color = entry.color;
                    GUI.DrawTexture(iconRect, entry.icon, ScaleMode.ScaleToFit, true);
                    GUI.color = previous;
                }
                if (entry.pinned && entry.kind != EntryKind.Tool)
                    EditorGUI.DrawRect(new Rect(iconRect.xMax - 5f, iconRect.y, 4f, 4f), new Color(1f, 0.85f, 0.3f, 0.95f));

                var labelRect = new Rect(rect.x + 28f, rect.y, rect.width * 0.5f, rect.height);
                GUI.Label(labelRect, entry.label, m_RowStyle);

                var right = new Rect(labelRect.xMax, rect.y, rect.width - labelRect.width - 30f, rect.height);
                string detail = string.IsNullOrEmpty(entry.note) ? entry.detail : entry.note + "  ·  " + entry.detail;
                GUI.Label(right, new GUIContent(detail, entry.path ?? entry.detail), m_PathStyle);
            }

            if (Event.current.type == EventType.MouseDown && hover && Event.current.button == 0)
            {
                Event.current.Use();
                Activate(entry, Event.current.clickCount >= 2);
            }
        }

        void HandleKeys()
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown) return;

            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    evt.Use();
                    Close();
                    break;
                case KeyCode.DownArrow:
                    evt.Use();
                    m_Index = Mathf.Min(m_Index + 1, Mathf.Max(0, m_Visible.Count - 1));
                    EnsureVisible();
                    Repaint();
                    break;
                case KeyCode.UpArrow:
                    evt.Use();
                    m_Index = Mathf.Max(m_Index - 1, 0);
                    EnsureVisible();
                    Repaint();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    evt.Use();
                    if (m_Index >= 0 && m_Index < m_Visible.Count)
                        Activate(m_Visible[m_Index], evt.shift);
                    break;
                case KeyCode.P:
                    if (evt.control || evt.command)
                    {
                        evt.Use();
                        if (m_Index >= 0 && m_Index < m_Visible.Count)
                            TogglePin(m_Visible[m_Index]);
                    }
                    break;
            }
        }

        void TogglePin(Entry entry)
        {
            switch (entry.kind)
            {
                case EntryKind.Asset:
                    if (entry.pinned) Navigator.Unpin(entry.guid);
                    else Navigator.Pin(entry.guid, false);
                    break;
                case EntryKind.Tool:
                    NavigatorTools.UnpinTool(entry.tool.id);
                    break;
                case EntryKind.Window:
                    if (entry.pinned)
                    {
                        var pin = NavigatorTools.AllToolPins().FirstOrDefault(t => t.pin.kind == ToolPinKind.Window && t.pin.windowType == NavigatorTools.TypeKey(entry.window.GetType())).pin;
                        if (pin != null) NavigatorTools.UnpinTool(pin.id);
                    }
                    else NavigatorTools.PinWindowType(entry.window.GetType(), false);
                    break;
                case EntryKind.RecentWindow:
                    var type = NavigatorTools.ResolveType(entry.recent.windowType);
                    if (type != null) NavigatorTools.PinWindowType(type, false);
                    break;
            }
            Collect();
            Repaint();
        }

        void EnsureVisible()
        {
            float rowTop = m_Index * k_RowHeight;
            float viewHeight = position.height - 34f;
            if (rowTop < m_Scroll.y) m_Scroll.y = rowTop;
            else if (rowTop + k_RowHeight > m_Scroll.y + viewHeight) m_Scroll.y = rowTop + k_RowHeight - viewHeight;
        }

        /// <summary>Enter / click. Folders open in the Project window, files are selected (Shift+Enter opens the asset), tools launch, windows get focus.</summary>
        void Activate(Entry entry, bool alternate)
        {
            Close();
            EditorApplication.delayCall += () =>
            {
                switch (entry.kind)
                {
                    case EntryKind.Asset:
                        if (alternate && !entry.isFolder) Navigator.OpenAsset(entry.guid);
                        else Navigator.Open(entry.guid);
                        break;
                    case EntryKind.Tool:
                        NavigatorTools.Launch(entry.tool);
                        break;
                    case EntryKind.Window:
                        NavigatorTools.Focus(entry.window);
                        break;
                    case EntryKind.RecentWindow:
                        var type = NavigatorTools.ResolveType(entry.recent.windowType);
                        if (type != null) NavigatorTools.FocusOrOpen(type);
                        else Debug.LogWarning($"[Project Navigator] Window type not found: {entry.recent.windowType}");
                        break;
                }
            };
        }

        void EnsureStyles()
        {
            if (m_RowStyle != null) return;
            m_RowStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            m_PathStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip,
                fontStyle = FontStyle.Italic
            };
            var c = m_PathStyle.normal.textColor;
            m_PathStyle.normal.textColor = new Color(c.r, c.g, c.b, 0.6f);
            m_SectionStyle = new GUIStyle(EditorStyles.miniBoldLabel) { padding = new RectOffset(6, 0, 4, 0) };
        }
    }
}
