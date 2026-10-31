// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// "Pin a tool": search every menu item in the editor (through Unity Search's menu provider),
    /// pick from the open windows, or type a menu path by hand.
    /// </summary>
    internal sealed class ToolPickerWindow : EditorWindow
    {
        const string k_SearchControl = "pn-tool-search";

        string m_Query = "";
        string m_CustomPath = "";
        bool m_Shared;
        bool m_FocusSearch = true;
        Vector2 m_Scroll;
        List<(string path, string label)> m_MenuResults = new List<(string path, string label)>();
        List<EditorWindow> m_Windows = new List<EditorWindow>();

        public static void Open()
        {
            var window = GetWindow<ToolPickerWindow>(true, "Pin a tool", true);
            window.minSize = new Vector2(460f, 420f);
            window.m_Shared = false;
            window.Refresh();
            window.Show();
            window.Focus();
        }

        void Refresh()
        {
            m_MenuResults = NavigatorTools.SearchMenuItems(m_Query, 60);
            var query = m_Query.Trim();
            m_Windows = NavigatorTools.OpenWindows()
                .GroupBy(w => w.GetType())
                .Select(g => g.First())
                .Where(w => query.Length == 0 || FuzzyMatcher.Score(query, w.titleContent.text) >= 0 || FuzzyMatcher.Score(query, w.GetType().Name) >= 0)
                .ToList();
        }

        void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
                Close();
                return;
            }

            EditorGUILayout.Space(4f);
            m_Shared = EditorGUILayout.ToggleLeft("Pin for the whole team (ProjectSettings) instead of just me (all my projects)", m_Shared);

            GUI.SetNextControlName(k_SearchControl);
            EditorGUI.BeginChangeCheck();
            m_Query = EditorGUILayout.TextField(m_Query, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck()) Refresh();
            if (m_FocusSearch)
            {
                m_FocusSearch = false;
                EditorGUI.FocusTextInControl(k_SearchControl);
            }
            EditorGUILayout.LabelField("Type at least two characters to search menu items (any Unity menu, including third-party tools).", EditorStyles.miniLabel);

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            EditorGUILayout.LabelField("Open windows", EditorStyles.boldLabel);
            if (m_Windows.Count == 0) EditorGUILayout.LabelField("No matching open windows.", EditorStyles.miniLabel);
            foreach (var window in m_Windows)
            {
                var type = window.GetType();
                bool pinned = NavigatorTools.IsWindowTypePinned(type);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var icon = ToolTracker.OriginalIcon(window);
                    GUILayout.Label(new GUIContent(icon), GUILayout.Width(18f), GUILayout.Height(18f));
                    EditorGUILayout.LabelField(new GUIContent(window.titleContent.text, type.FullName), GUILayout.MinWidth(80f));
                    GUILayout.Label(type.Name, EditorStyles.miniLabel, GUILayout.Width(160f));
                    using (new EditorGUI.DisabledScope(pinned))
                    {
                        if (GUILayout.Button(pinned ? "Pinned" : "Pin", EditorStyles.miniButton, GUILayout.Width(58f)))
                            NavigatorTools.PinWindowType(type, m_Shared);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Menu items", EditorStyles.boldLabel);
            if (m_MenuResults.Count == 0)
                EditorGUILayout.LabelField(m_Query.Trim().Length < 2 ? "Search above." : "No matching menu items.", EditorStyles.miniLabel);
            foreach (var (path, label) in m_MenuResults)
            {
                bool pinned = NavigatorTools.IsMenuPinned(path);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent(string.IsNullOrEmpty(label) ? path : label, path), GUILayout.MinWidth(80f));
                    GUILayout.Label(path, EditorStyles.miniLabel, GUILayout.Width(220f));
                    using (new EditorGUI.DisabledScope(pinned))
                    {
                        if (GUILayout.Button(pinned ? "Pinned" : "Pin", EditorStyles.miniButton, GUILayout.Width(58f)))
                            NavigatorTools.PinMenuItem(path, label, m_Shared);
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Custom menu path", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                m_CustomPath = EditorGUILayout.TextField(m_CustomPath);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(m_CustomPath)))
                {
                    if (GUILayout.Button("Pin", GUILayout.Width(58f)))
                    {
                        NavigatorTools.PinMenuItem(m_CustomPath, "", m_Shared);
                        m_CustomPath = "";
                        GUI.FocusControl(null);
                    }
                }
            }
            EditorGUILayout.LabelField("Example: Window/Analysis/Profiler", EditorStyles.miniLabel);
        }
    }
}
