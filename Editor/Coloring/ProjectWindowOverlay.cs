// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Draws colors, pin badges and notes on top of Project window items using the public per-item
    /// callback. Nothing in the project or in .meta files is modified; this is purely cosmetic.
    ///
    /// Layout heuristics (tune here if something is misaligned on your icon size):
    ///  - list/tree rows are wider than tall: the icon is the left square of the row,
    ///  - grid tiles are taller than wide: the icon is the top square, the label sits below it.
    /// </summary>
    [InitializeOnLoad]
    internal static class ProjectWindowOverlay
    {
        static readonly Color k_PinBadgeDark = new Color(1f, 0.85f, 0.3f, 0.95f);
        static readonly Color k_PinBadgeLight = new Color(0.85f, 0.55f, 0.05f, 0.95f);
        static GUIStyle s_NoteStyle;

        static ProjectWindowOverlay()
        {
            EditorApplication.projectWindowItemOnGUI -= OnItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnItemGUI;
        }

        static void OnItemGUI(string guid, Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;

            var user = NavigatorUserState.instance;
            if (!user.overlayEnabled) return;
            if (!FolderColorResolver.TryGet(guid, out var style)) return;

            bool listRow = rect.width > rect.height;
            Rect iconRect = listRow
                ? new Rect(rect.x, rect.y, rect.height, rect.height)
                : new Rect(rect.x, rect.y, rect.width, rect.width);

            if (style.isFolder)
            {
                if (style.hasColor && style.icon != null)
                {
                    var previous = GUI.color;
                    GUI.color = style.color;
                    GUI.DrawTexture(iconRect, style.icon, ScaleMode.ScaleToFit, true);
                    GUI.color = previous;
                }
            }
            else if (style.hasColor)
            {
                // Files keep their own icon; the color is a bar at the right edge (list) or under the icon (grid).
                float thickness = style.explicitOnFile ? 3f : 2f;
                var bar = listRow
                    ? new Rect(rect.xMax - thickness - 1f, rect.y + 2f, thickness, rect.height - 4f)
                    : new Rect(iconRect.x, iconRect.yMax - thickness, iconRect.width, thickness);
                var c = style.color;
                c.a = style.explicitOnFile ? 1f : 0.9f;
                EditorGUI.DrawRect(bar, c);
            }

            if (style.pinned && user.showPinBadge)
            {
                float size = listRow ? 5f : Mathf.Clamp(iconRect.width * 0.18f, 5f, 10f);
                var badge = new Rect(iconRect.xMax - size - 1f, iconRect.y + 1f, size, size);
                EditorGUI.DrawRect(badge, EditorGUIUtility.isProSkin ? k_PinBadgeDark : k_PinBadgeLight);
            }

            if (listRow && style.HasNote && user.showNotesInProjectWindow)
                DrawNote(rect, style);
        }

        /// <summary>Right-aligned, dimmed note; skipped when the row is too narrow for the item name plus the note.</summary>
        static void DrawNote(Rect rect, FolderStyle style)
        {
            if (s_NoteStyle == null)
            {
                s_NoteStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    clipping = TextClipping.Clip,
                    fontStyle = FontStyle.Italic
                };
            }
            if (style.nameWidth < 0f)
                style.nameWidth = EditorStyles.label.CalcSize(new GUIContent(style.name)).x;

            float noteWidth = s_NoteStyle.CalcSize(style.noteContent).x;
            float left = rect.x + rect.height + 6f + style.nameWidth + 16f;
            float right = rect.xMax - 8f;
            float available = right - left;
            if (available < 40f) return;

            float width = Mathf.Min(noteWidth, available);
            var noteRect = new Rect(right - width, rect.y, width, rect.height);
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(noteRect, style.noteContent, s_NoteStyle);
            GUI.color = previous;
        }
    }
}
