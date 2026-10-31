// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Adds a small strip under the Inspector header for folders (and, optionally, for files):
    /// color swatch, tag dropdown, Pin toggle, Open button and the asset's note.
    /// Uses Editor.finishedDefaultHeaderGUI, so it does not replace any inspector and does not
    /// conflict with other packages that customize DefaultAsset.
    /// </summary>
    [InitializeOnLoad]
    internal static class InspectorHeaderStrip
    {
        static readonly GUIContent k_PinOn = new GUIContent("Pinned", "Unpin");
        static readonly GUIContent k_PinOff = new GUIContent("Pin", "Pin (personal pin)");
        static readonly GUIContent k_Open = new GUIContent("Open", "Show in the Project window");
        static readonly GUIContent k_Note = new GUIContent("Note", "Set a short note shown next to the path");

        static InspectorHeaderStrip()
        {
            Editor.finishedDefaultHeaderGUI -= OnHeaderGUI;
            Editor.finishedDefaultHeaderGUI += OnHeaderGUI;
        }

        static void DrawSceneObjectStrip(GameObject gameObject, NavigatorUserState user)
        {
            if (!SceneNavigator.TryGetId(gameObject, out var id, out _)) return; // runtime objects, prefab contents, unsaved scenes
            var shared = NavigatorSharedSettings.instance;

            var options = new string[shared.tags.Count + 2];
            options[0] = "Color: inherit";
            options[1] = "Color: none";
            for (int i = 0; i < shared.tags.Count; i++)
                options[i + 2] = shared.tags[i].name;

            int current = 0;
            var explicitColor = SceneNavigator.FindExplicitColor(id, out _);
            if (explicitColor != null)
            {
                if (string.IsNullOrEmpty(explicitColor.tagId)) current = 1;
                else
                {
                    int index = shared.tags.FindIndex(t => t.id == explicitColor.tagId);
                    current = index >= 0 ? index + 2 : 0;
                }
            }

            var style = SceneNavigator.StyleFor(gameObject);
            var note = SceneNavigator.GetNote(id);

            using (new EditorGUILayout.HorizontalScope())
            {
                var swatch = GUILayoutUtility.GetRect(12f, 12f, GUILayout.Width(12f), GUILayout.Height(12f));
                swatch.y += 3f;
                EditorGUI.DrawRect(swatch, style.hasColor ? style.color : new Color(0.5f, 0.5f, 0.5f, 0.25f));

                int next = EditorGUILayout.Popup(current, options, GUILayout.MaxWidth(160f));
                if (next != current)
                {
                    if (next == 0) SceneNavigator.ClearColor(id);
                    else SceneNavigator.SetColor(id, next == 1 ? "" : shared.tags[next - 2].id, user.colorScopeShared);
                }

                bool pinned = SceneNavigator.IsPinned(id);
                if (GUILayout.Button(pinned ? k_PinOn : k_PinOff, EditorStyles.miniButton, GUILayout.Width(54f)))
                {
                    if (pinned) SceneNavigator.Unpin(id);
                    else SceneNavigator.Pin(gameObject, false, out _);
                }

                if (GUILayout.Button(new GUIContent("Frame", "Frame in the Scene view"), EditorStyles.miniButton, GUILayout.Width(50f)))
                {
                    var targetId = id;
                    EditorApplication.delayCall += () => SceneNavigator.Frame(targetId);
                }

                if (GUILayout.Button(k_Note, EditorStyles.miniButton, GUILayout.Width(44f)))
                {
                    var targetId = id;
                    bool toShared = user.colorScopeShared;
                    TextPromptWindow.Prompt("Note", "Note", note, value => SceneNavigator.SetNote(targetId, value, toShared));
                }

                if (!string.IsNullOrEmpty(note))
                    GUILayout.Label(new GUIContent(note, note), EditorStyles.miniLabel, GUILayout.MinWidth(20f));

                GUILayout.FlexibleSpace();
                GUILayout.Label(user.colorScopeShared ? "team" : "personal", EditorStyles.miniLabel);
            }
        }

        static void OnHeaderGUI(Editor editor)
        {
            if (editor == null || editor.targets == null || editor.targets.Length != 1) return;
            var target = editor.target;
            if (target == null) return;

            var user = NavigatorUserState.instance;
            if (!user.inspectorStripEnabled) return;

            if (target is GameObject gameObject && !EditorUtility.IsPersistent(gameObject))
            {
                if (user.inspectorStripForGameObjects) DrawSceneObjectStrip(gameObject, user);
                return;
            }

            bool isFolder = target is DefaultAsset;
            if (!isFolder)
            {
                if (!user.inspectorStripForFiles) return;
                if (!EditorUtility.IsPersistent(target) || !AssetDatabase.IsMainAsset(target)) return;
            }

            var path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path)) return;
            if (isFolder && !AssetDatabase.IsValidFolder(path)) return;

            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return;

            var shared = NavigatorSharedSettings.instance;

            // Dropdown options: 0 = inherit/rules, 1 = explicit none, 2.. = tags
            var options = new string[shared.tags.Count + 2];
            options[0] = isFolder ? "Color: inherit / rules" : "Color: from folder";
            options[1] = "Color: none";
            for (int i = 0; i < shared.tags.Count; i++)
                options[i + 2] = shared.tags[i].name;

            int current = 0;
            var explicitColor = Navigator.FindExplicitColor(guid, out _);
            if (explicitColor != null)
            {
                if (string.IsNullOrEmpty(explicitColor.tagId))
                {
                    current = 1;
                }
                else
                {
                    int index = shared.tags.FindIndex(t => t.id == explicitColor.tagId);
                    current = index >= 0 ? index + 2 : 0;
                }
            }

            FolderColorResolver.TryGet(guid, out var style);
            var note = Navigator.GetNote(guid);

            using (new EditorGUILayout.HorizontalScope())
            {
                var swatch = GUILayoutUtility.GetRect(12f, 12f, GUILayout.Width(12f), GUILayout.Height(12f));
                swatch.y += 3f;
                EditorGUI.DrawRect(swatch, style.hasColor ? style.color : new Color(0.5f, 0.5f, 0.5f, 0.25f));

                int next = EditorGUILayout.Popup(current, options, GUILayout.MaxWidth(160f));
                if (next != current)
                {
                    if (next == 0) Navigator.ClearColor(guid);
                    else Navigator.SetColor(guid, next == 1 ? "" : shared.tags[next - 2].id, user.colorScopeShared);
                }

                bool pinned = Navigator.IsPinned(guid);
                if (GUILayout.Button(pinned ? k_PinOn : k_PinOff, EditorStyles.miniButton, GUILayout.Width(54f)))
                {
                    if (pinned) Navigator.Unpin(guid);
                    else Navigator.Pin(guid, false);
                }

                if (GUILayout.Button(k_Open, EditorStyles.miniButton, GUILayout.Width(46f)))
                {
                    // Changing the selection inside an inspector draw call is unsafe; defer it.
                    var targetGuid = guid;
                    EditorApplication.delayCall += () => Navigator.Open(targetGuid);
                }

                if (GUILayout.Button(k_Note, EditorStyles.miniButton, GUILayout.Width(44f)))
                {
                    var targetGuid = guid;
                    bool toShared = user.colorScopeShared;
                    TextPromptWindow.Prompt("Note", "Note", note, value => Navigator.SetNote(targetGuid, value, toShared));
                }

                if (!string.IsNullOrEmpty(note))
                    GUILayout.Label(new GUIContent(note, note), EditorStyles.miniLabel, GUILayout.MinWidth(20f));

                GUILayout.FlexibleSpace();
                GUILayout.Label(user.colorScopeShared ? "team" : "personal", EditorStyles.miniLabel);
            }
        }
    }
}
