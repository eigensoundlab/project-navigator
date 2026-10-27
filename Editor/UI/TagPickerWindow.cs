// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// "Custom..." color picker for one or more folders: pick an existing tag, or create a new tag
    /// with its own color and apply it in one go.
    /// </summary>
    internal sealed class TagPickerWindow : EditorWindow
    {
        string[] m_Guids;
        bool m_Shared;
        Vector2 m_Scroll;
        string m_NewTagName = "";
        Color m_NewTagColor = new Color(0.4f, 0.7f, 1f, 1f);
        bool m_NewTagInherit = true;

        public static void Open(string[] folderGuids)
        {
            if (folderGuids == null || folderGuids.Length == 0) return;
            var window = GetWindow<TagPickerWindow>(true, "Folder Color", true);
            window.m_Guids = folderGuids;
            window.m_Shared = NavigatorUserState.instance.colorScopeShared;
            window.minSize = new Vector2(300f, 360f);
            window.Show();
        }

        void OnGUI()
        {
            if (m_Guids == null || m_Guids.Length == 0)
            {
                EditorGUILayout.HelpBox("No folders selected.", MessageType.Info);
                return;
            }

            var shared = NavigatorSharedSettings.instance;

            EditorGUILayout.LabelField(m_Guids.Length == 1 ? Navigator.GetInfo(m_Guids[0]).path : $"{m_Guids.Length} assets selected", EditorStyles.boldLabel);
            m_Shared = EditorGUILayout.ToggleLeft("Save for the whole team (ProjectSettings)", m_Shared);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Pick a tag", EditorStyles.boldLabel);
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < shared.tags.Count; i++)
            {
                var tag = shared.tags[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    var swatch = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f), GUILayout.Height(16f));
                    swatch.y += 2f;
                    EditorGUI.DrawRect(swatch, tag.color);
                    if (GUILayout.Button(tag.name, EditorStyles.miniButton))
                        Apply(tag.id);
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("None (block inheritance)", EditorStyles.miniButtonLeft))
                    Apply("");
                if (GUILayout.Button("Clear (rules / inheritance)", EditorStyles.miniButtonRight))
                {
                    foreach (var guid in m_Guids) Navigator.ClearColor(guid);
                    Close();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("New tag", EditorStyles.boldLabel);
            m_NewTagName = EditorGUILayout.TextField("Name", m_NewTagName);
            m_NewTagColor = EditorGUILayout.ColorField("Color", m_NewTagColor);
            m_NewTagInherit = EditorGUILayout.Toggle("Inherit to sub-folders", m_NewTagInherit);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(m_NewTagName)))
            {
                if (GUILayout.Button("Create tag and apply"))
                {
                    var tag = new ColorTag { name = m_NewTagName.Trim(), color = m_NewTagColor, inheritToChildren = m_NewTagInherit };
                    shared.tags.Add(tag);
                    Navigator.MarkDirty(shared: true);
                    Apply(tag.id);
                }
            }
        }

        void Apply(string tagId)
        {
            foreach (var guid in m_Guids)
                Navigator.SetColor(guid, tagId, m_Shared);
            Close();
            GUIUtility.ExitGUI();
        }
    }
}
