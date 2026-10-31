// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>Preset swatches plus a color field, for tool pins and window colors (which are not tag-based).</summary>
    internal sealed class ColorPromptWindow : EditorWindow
    {
        Color m_Value;
        Action<Color?> m_OnAccept;

        public static void Prompt(string title, Color initial, Action<Color?> onAccept)
        {
            var window = CreateInstance<ColorPromptWindow>();
            window.titleContent = new GUIContent(title);
            window.m_Value = initial;
            window.m_OnAccept = onAccept;
            window.minSize = new Vector2(300f, 118f);
            window.maxSize = new Vector2(300f, 118f);
            window.ShowUtility();
            window.Focus();
        }

        void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
                Close();
                return;
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < NavigatorTools.PresetColors.Length; i++)
                {
                    var color = NavigatorTools.PresetColor(i);
                    var rect = GUILayoutUtility.GetRect(26f, 20f, GUILayout.Width(26f), GUILayout.Height(20f));
                    EditorGUI.DrawRect(rect, color);
                    if (GUI.Button(rect, new GUIContent("", NavigatorTools.PresetColors[i].name), GUIStyle.none))
                        Accept(color);
                }
            }
            m_Value = EditorGUILayout.ColorField("Custom", m_Value);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("No color", GUILayout.Width(80f))) Accept(null);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(80f))) Close();
                if (GUILayout.Button("OK", GUILayout.Width(80f))) Accept(m_Value);
            }
        }

        void Accept(Color? value)
        {
            var callback = m_OnAccept;
            Close();
            callback?.Invoke(value);
            GUIUtility.ExitGUI();
        }
    }
}
