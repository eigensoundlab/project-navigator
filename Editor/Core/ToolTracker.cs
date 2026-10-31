// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectNavigator
{
    /// <summary>
    /// Tracks which editor windows get focus ("Recent windows") and applies window marks:
    /// a colored dot as the tab icon and a thin colored strip at the top of the window content.
    /// Both are applied through public API (titleContent, rootVisualElement) and re-applied
    /// periodically because some windows reset their title or rebuild their root.
    /// </summary>
    [InitializeOnLoad]
    internal static class ToolTracker
    {
        const string k_StripName = "pn-window-strip";
        const string k_DotName = "pn-window-dot";

        static double s_NextFocusPoll;
        static double s_NextMarkPoll;
        static EditorWindow s_LastForeign;
        static readonly Dictionary<int, GUIContent> s_OriginalTitles = new Dictionary<int, GUIContent>();
        static readonly Dictionary<Color, Texture2D> s_Dots = new Dictionary<Color, Texture2D>();

        static ToolTracker()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        /// <summary>The most recently focused window that is not part of the Navigator (for "pin focused window").</summary>
        public static EditorWindow LastForeignWindow => s_LastForeign != null ? s_LastForeign : null;

        static void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now >= s_NextFocusPoll)
            {
                s_NextFocusPoll = now + 0.3;
                PollFocus();
            }
            if (now >= s_NextMarkPoll)
            {
                s_NextMarkPoll = now + 1.0;
                ApplyMarks();
            }
        }

        static void PollFocus()
        {
            var window = EditorWindow.focusedWindow;
            if (window == null || NavigatorTools.IsNavigatorWindow(window)) return;
            if (window == s_LastForeign) return;
            s_LastForeign = window;
            if (NavigatorGlobal.Data.trackWindows)
                NavigatorTools.RecordRecent(window);
        }

        public static void ApplyMarksNow()
        {
            ApplyMarks();
        }

        static void ApplyMarks()
        {
            var data = NavigatorGlobal.Data;
            foreach (var window in NavigatorTools.OpenWindows())
            {
                try
                {
                    var mark = NavigatorTools.FindMark(window);
                    bool colored = mark != null && mark.hasColor;
                    if (colored && data.colorWindowStrips) EnsureStrip(window, mark.color);
                    else RemoveStrip(window);
                    if (colored && data.colorWindowTabs) EnsureIcon(window, mark.color);
                    else RestoreIcon(window);
                }
                catch (Exception)
                {
                    // A window that refuses decoration is left alone.
                }
            }
        }

        /// <summary>The window's own icon, even while the tab shows a color dot.</summary>
        public static Texture OriginalIcon(EditorWindow window)
        {
            if (window == null) return null;
            var current = window.titleContent != null ? window.titleContent.image : null;
            if (IsDot(current) && s_OriginalTitles.TryGetValue(window.GetHashCode(), out var original))
                return original.image;
            return IsDot(current) ? null : current;
        }

        static void EnsureStrip(EditorWindow window, Color color)
        {
            var root = window.rootVisualElement;
            if (root == null) return;
            var strip = root.Q(k_StripName);
            if (strip == null)
            {
                strip = new VisualElement { name = k_StripName, pickingMode = PickingMode.Ignore };
                strip.style.height = 3;
                strip.style.flexShrink = 0;
                strip.style.flexGrow = 0;
                root.Insert(0, strip);
            }
            strip.style.backgroundColor = color;
        }

        static void RemoveStrip(EditorWindow window)
        {
            var root = window.rootVisualElement;
            root?.Q(k_StripName)?.RemoveFromHierarchy();
        }

        static void EnsureIcon(EditorWindow window, Color color)
        {
            var dot = Dot(color);
            var current = window.titleContent;
            if (current.image == dot) return;
            int key = window.GetHashCode();
            if (!IsDot(current.image))
                s_OriginalTitles[key] = new GUIContent(current);
            window.titleContent = new GUIContent(current.text, dot, current.tooltip);
        }

        static void RestoreIcon(EditorWindow window)
        {
            var current = window.titleContent;
            int key = window.GetHashCode();
            if (!IsDot(current.image))
            {
                s_OriginalTitles.Remove(key);
                return;
            }
            if (s_OriginalTitles.TryGetValue(key, out var original))
            {
                window.titleContent = new GUIContent(current.text, original.image, original.tooltip);
                s_OriginalTitles.Remove(key);
            }
            else
            {
                window.titleContent = new GUIContent(current.text, (Texture)null, current.tooltip);
            }
        }

        static bool IsDot(Texture texture)
        {
            return texture != null && texture.name == k_DotName;
        }

        static Texture2D Dot(Color color)
        {
            if (s_Dots.TryGetValue(color, out var texture) && texture != null) return texture;
            const int size = 16;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = k_DotName,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.34f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float alpha = Mathf.Clamp01(radius + 0.5f - d);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            s_Dots[color] = texture;
            return texture;
        }
    }
}
