// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// "Custom..." color picker: pick an existing tag, or create a new tag with its own color and apply it
    /// in one go. Works for assets (by GUID) and for scene objects through the apply/clear callbacks.
    /// </summary>
    internal sealed class TagPickerWindow : EditorWindow
    {
        string m_Header = "";
        bool m_Shared;
        Vector2 m_Scroll;
        string m_NewTagName = "";
        Color m_NewTagColor = new Color(0.4f, 0.7f, 1f, 1f);
        bool m_NewTagInherit = true;
        Action<string, bool> m_Apply;
        Action m_Clear;

        /// <summary>Assets: the callbacks color/clear every GUID.</summary>
        public static void Open(string[] assetGuids)
        {
            if (assetGuids == null || assetGuids.Length == 0) return;
            string header = assetGuids.Length == 1 ? Navigator.GetInfo(assetGuids[0]).path : $"{assetGuids.Length} assets selected";
            OpenFor(header,
                (tagId, shared) =>
                {
                    foreach (var guid in assetGuids) Navigator.SetColor(guid, tagId, shared);
                },
                () =>
                {
                    foreach (var guid in assetGuids) Navigator.ClearColor(guid);
                });
        }

        public static void OpenFor(string header, Action<string, bool> apply, Action clear)
        {
            var window = GetWindow<TagPickerWindow>(true, "Color", true);
            window.m_Header = header ?? "";
            window.m_Apply = apply;
            window.m_Clear = clear;
            window.m_Shared = NavigatorUserState.instance.colorScopeShared;
            window.minSize = new Vector2(300f, 360f);
            window.Show();
        }

        void OnGUI()
        {
            if (m_Apply == null)
            {
                EditorGUILayout.HelpBox("Nothing selected.", MessageType.Info);
                return;
            }

            var shared = NavigatorSharedSettings.instance;

            EditorGUILayout.LabelField(m_Header, EditorStyles.boldLabel);
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
                    m_Clear?.Invoke();
                    Close();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("New tag", EditorStyles.boldLabel);
            m_NewTagName = EditorGUILayout.TextField("Name", m_NewTagName);
            m_NewTagColor = EditorGUILayout.ColorField("Color", m_NewTagColor);
            m_NewTagInherit = EditorGUILayout.Toggle("Inherit to children", m_NewTagInherit);
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
            m_Apply?.Invoke(tagId, m_Shared);
            Close();
            GUIUtility.ExitGUI();
        }
    }
}
