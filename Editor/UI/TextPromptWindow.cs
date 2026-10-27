// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>Minimal single-line text prompt (Unity has no built-in input dialog).</summary>
    internal sealed class TextPromptWindow : EditorWindow
    {
        string m_Label;
        string m_Value;
        Action<string> m_OnAccept;
        bool m_Focused;

        public static void Prompt(string title, string label, string initialValue, Action<string> onAccept)
        {
            var window = CreateInstance<TextPromptWindow>();
            window.titleContent = new GUIContent(title);
            window.m_Label = label;
            window.m_Value = initialValue ?? "";
            window.m_OnAccept = onAccept;
            window.minSize = new Vector2(340f, 64f);
            window.maxSize = new Vector2(340f, 64f);
            window.ShowUtility();
            window.Focus();
        }

        void OnGUI()
        {
            var evt = Event.current;
            if (evt.type == EventType.KeyDown)
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    evt.Use();
                    Accept();
                    return;
                }
                if (evt.keyCode == KeyCode.Escape)
                {
                    evt.Use();
                    Close();
                    return;
                }
            }

            EditorGUILayout.Space(6f);
            GUI.SetNextControlName("pn-prompt");
            m_Value = EditorGUILayout.TextField(m_Label, m_Value);
            if (!m_Focused)
            {
                m_Focused = true;
                EditorGUI.FocusTextInControl("pn-prompt");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(80f))) Close();
                if (GUILayout.Button("OK", GUILayout.Width(80f))) Accept();
            }
        }

        void Accept()
        {
            var callback = m_OnAccept;
            var value = m_Value;
            Close();
            callback?.Invoke(value);
        }
    }
}
