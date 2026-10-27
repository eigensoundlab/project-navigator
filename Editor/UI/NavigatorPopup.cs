// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// "Popup at cursor": a compact panel summoned by a shortcut (default Alt+Shift+Space) at the mouse
    /// position. It lists pinned, frequent and recent folders (and recent files), filters as you type,
    /// opens the highlighted entry with Enter, and closes when it loses focus or on Escape.
    /// Because it only exists while it has focus, window stacking never gets in its way.
    /// </summary>
    internal sealed class NavigatorPopup : EditorWindow
    {
        sealed class Entry
        {
            public string guid;
            public string path;
            public string label;
            public string note;
            public string section;
            public bool isFolder;
            public bool pinned;
            public bool hasColor;
            public Color color;
            public Texture icon;
        }

        const float k_Width = 380f;
        const float k_RowHeight = 20f;
        const int k_MaxVisibleRows = 18;
        const string k_SearchControl = "pn-popup-search";

        static NavigatorPopup s_Instance;

        readonly List<Entry> m_All = new List<Entry>();
        readonly List<Entry> m_Visible = new List<Entry>();
        string m_Filter = "";
        int m_Index;
        Vector2 m_Scroll;
        bool m_FocusSearch = true;
        GUIStyle m_RowStyle;
        GUIStyle m_PathStyle;
        GUIStyle m_SectionStyle;

        public static void Toggle()
        {
            if (s_Instance != null)
            {
                s_Instance.Close();
                return;
            }
            Open();
        }

        public static void Open()
        {
            if (s_Instance != null)
            {
                s_Instance.Focus();
                return;
            }

            var window = CreateInstance<NavigatorPopup>();
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
                catch (System.Exception)
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
            // Closing from inside the focus callback is unsafe on some platforms; defer by one tick.
            EditorApplication.delayCall += () =>
            {
                if (this != null) Close();
            };
        }

        void Collect()
        {
            m_All.Clear();
            var user = NavigatorUserState.instance;
            var seen = new HashSet<string>();

            foreach (var (pin, shared) in Navigator.AllPins())
            {
                var info = Navigator.GetInfo(pin.guid);
                if (!info.Exists || !seen.Add(info.guid)) continue;
                var entry = Make(info, "Pinned" + (shared ? " (team)" : ""));
                if (!string.IsNullOrEmpty(pin.label)) entry.label = pin.label;
                m_All.Add(entry);
            }
            foreach (var (info, _) in Navigator.Frequent(user.frequentCount))
            {
                if (!seen.Add(info.guid)) continue;
                m_All.Add(Make(info, "Frequent"));
            }
            foreach (var info in Navigator.Recent(user.recentCount))
            {
                if (!seen.Add(info.guid)) continue;
                m_All.Add(Make(info, "Recent"));
            }
            if (user.trackFiles)
            {
                foreach (var info in Navigator.RecentFiles(user.recentFilesCount))
                {
                    if (!seen.Add(info.guid)) continue;
                    m_All.Add(Make(info, "Recent files"));
                }
            }
            ApplyFilter();
        }

        static Entry Make(AssetInfo info, string section)
        {
            var entry = new Entry
            {
                guid = info.guid,
                path = info.path,
                label = info.name,
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
                    int pathScore = FuzzyMatcher.Score(m_Filter, entry.path);
                    if (pathScore - 2 > score) score = pathScore - 2;
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

        void OnGUI()
        {
            EnsureStyles();
            HandleKeys();

            // Border so the popup reads as a panel on both skins.
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
                GUILayout.Label(m_All.Count == 0 ? "Nothing pinned or visited yet." : "No match.", EditorStyles.centeredGreyMiniLabel);
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
                if (entry.pinned)
                    EditorGUI.DrawRect(new Rect(iconRect.xMax - 5f, iconRect.y, 4f, 4f), new Color(1f, 0.85f, 0.3f, 0.95f));

                var labelRect = new Rect(rect.x + 28f, rect.y, rect.width * 0.5f, rect.height);
                GUI.Label(labelRect, entry.label, m_RowStyle);

                var right = new Rect(labelRect.xMax, rect.y, rect.width - labelRect.width - 30f, rect.height);
                string detail = string.IsNullOrEmpty(entry.note) ? Navigator.ParentOf(entry.path) : entry.note + "  ·  " + Navigator.ParentOf(entry.path);
                GUI.Label(right, new GUIContent(detail, entry.path), m_PathStyle);
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
                        {
                            var entry = m_Visible[m_Index];
                            if (entry.pinned) Navigator.Unpin(entry.guid);
                            else Navigator.Pin(entry.guid, false);
                            Collect();
                            Repaint();
                        }
                    }
                    break;
            }
        }

        void EnsureVisible()
        {
            float rowTop = m_Index * k_RowHeight;
            float viewHeight = position.height - 34f;
            if (rowTop < m_Scroll.y) m_Scroll.y = rowTop;
            else if (rowTop + k_RowHeight > m_Scroll.y + viewHeight) m_Scroll.y = rowTop + k_RowHeight - viewHeight;
        }

        /// <summary>Enter / click: folders open in the Project window, files are selected. Shift+Enter / double-click on a file opens the asset.</summary>
        void Activate(Entry entry, bool openAsset)
        {
            var guid = entry.guid;
            Close();
            EditorApplication.delayCall += () =>
            {
                if (openAsset && !entry.isFolder) Navigator.OpenAsset(guid);
                else Navigator.Open(guid);
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
            m_PathStyle.normal.textColor = new Color(m_PathStyle.normal.textColor.r, m_PathStyle.normal.textColor.g, m_PathStyle.normal.textColor.b, 0.6f);
            m_SectionStyle = new GUIStyle(EditorStyles.miniBoldLabel) { padding = new RectOffset(6, 0, 4, 0) };
        }
    }
}
