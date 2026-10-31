// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Right-click entries in the Hierarchy (and the GameObject menu): GameObject → Project Navigator → ...
    /// Unity invokes GameObject menu items once per selected object; every entry runs once, on the whole selection.
    /// </summary>
    internal static class SceneContextMenus
    {
        const string k_Root = "GameObject/Project Navigator/";
        const int k_Priority = 12;

        static bool First(MenuCommand command)
        {
            var selected = Selection.gameObjects;
            return selected.Length == 0 || command.context == null || command.context == selected[0];
        }

        static bool AnySelected()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(k_Root + "Pin", false, k_Priority)]
        static void Pin(MenuCommand command)
        {
            if (First(command)) SceneNavigator.PinSelection(false);
        }

        [MenuItem(k_Root + "Pin", true)]
        static bool PinValidate() => AnySelected();

        [MenuItem(k_Root + "Pin (Team)", false, k_Priority + 1)]
        static void PinTeam(MenuCommand command)
        {
            if (First(command)) SceneNavigator.PinSelection(true);
        }

        [MenuItem(k_Root + "Pin (Team)", true)]
        static bool PinTeamValidate() => AnySelected();

        [MenuItem(k_Root + "Unpin", false, k_Priority + 2)]
        static void Unpin(MenuCommand command)
        {
            if (!First(command)) return;
            foreach (var go in Selection.gameObjects)
                if (SceneNavigator.TryGetId(go, out var id, out _)) SceneNavigator.Unpin(id);
        }

        [MenuItem(k_Root + "Unpin", true)]
        static bool UnpinValidate()
        {
            foreach (var go in Selection.gameObjects)
                if (SceneNavigator.TryGetId(go, out var id, out _) && SceneNavigator.IsPinned(id)) return true;
            return false;
        }

        [MenuItem(k_Root + "Note...", false, k_Priority + 10)]
        static void Note(MenuCommand command)
        {
            if (!First(command)) return;
            var selected = Selection.gameObjects;
            if (selected.Length == 0) return;
            bool toShared = NavigatorUserState.instance.colorScopeShared;
            string initial = "";
            if (selected.Length == 1 && SceneNavigator.TryGetId(selected[0], out var firstId, out _))
                initial = SceneNavigator.GetNote(firstId);
            TextPromptWindow.Prompt("Note", selected.Length == 1 ? "Note" : $"Note for {selected.Length} objects", initial, value =>
            {
                string firstError = null;
                foreach (var go in selected)
                {
                    if (SceneNavigator.TryGetId(go, out var id, out var error)) SceneNavigator.SetNote(id, value, toShared);
                    else if (firstError == null) firstError = error;
                }
                if (firstError != null) EditorUtility.DisplayDialog("Project Navigator", firstError, "OK");
            });
        }

        [MenuItem(k_Root + "Note...", true)]
        static bool NoteValidate() => AnySelected();

        [MenuItem(k_Root + "Clear Note", false, k_Priority + 11)]
        static void ClearNote(MenuCommand command)
        {
            if (!First(command)) return;
            foreach (var go in Selection.gameObjects)
                if (SceneNavigator.TryGetId(go, out var id, out _)) SceneNavigator.ClearNote(id);
        }

        [MenuItem(k_Root + "Clear Note", true)]
        static bool ClearNoteValidate() => AnySelected();

        // ---------------------------------------------------------------- colors

        static void ApplyDefaultTag(MenuCommand command, string tagId)
        {
            if (!First(command)) return;
            if (NavigatorSharedSettings.instance.EnsureDefaultTag(tagId) == null) return;
            SceneNavigator.ColorSelection(tagId, NavigatorUserState.instance.colorScopeShared);
        }

        [MenuItem(k_Root + "Color/Yellow", false, k_Priority + 40)] static void ColorYellow(MenuCommand c) => ApplyDefaultTag(c, NavigatorSharedSettings.DefaultTags.Yellow);
        [MenuItem(k_Root + "Color/Yellow", true)] static bool ColorYellowValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Green", false, k_Priority + 41)] static void ColorGreen(MenuCommand c) => ApplyDefaultTag(c, NavigatorSharedSettings.DefaultTags.Green);
        [MenuItem(k_Root + "Color/Green", true)] static bool ColorGreenValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Blue", false, k_Priority + 42)] static void ColorBlue(MenuCommand c) => ApplyDefaultTag(c, NavigatorSharedSettings.DefaultTags.Blue);
        [MenuItem(k_Root + "Color/Blue", true)] static bool ColorBlueValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Orange", false, k_Priority + 43)] static void ColorOrange(MenuCommand c) => ApplyDefaultTag(c, NavigatorSharedSettings.DefaultTags.Orange);
        [MenuItem(k_Root + "Color/Orange", true)] static bool ColorOrangeValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Red", false, k_Priority + 44)] static void ColorRed(MenuCommand c) => ApplyDefaultTag(c, NavigatorSharedSettings.DefaultTags.Red);
        [MenuItem(k_Root + "Color/Red", true)] static bool ColorRedValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Purple", false, k_Priority + 45)] static void ColorPurple(MenuCommand c) => ApplyDefaultTag(c, NavigatorSharedSettings.DefaultTags.Purple);
        [MenuItem(k_Root + "Color/Purple", true)] static bool ColorPurpleValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Teal", false, k_Priority + 46)] static void ColorTeal(MenuCommand c) => ApplyDefaultTag(c, NavigatorSharedSettings.DefaultTags.Teal);
        [MenuItem(k_Root + "Color/Teal", true)] static bool ColorTealValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Grey", false, k_Priority + 47)] static void ColorGrey(MenuCommand c) => ApplyDefaultTag(c, NavigatorSharedSettings.DefaultTags.Grey);
        [MenuItem(k_Root + "Color/Grey", true)] static bool ColorGreyValidate() => AnySelected();

        [MenuItem(k_Root + "Color/Custom...", false, k_Priority + 60)]
        static void ColorCustom(MenuCommand command)
        {
            if (!First(command)) return;
            var selected = Selection.gameObjects;
            if (selected.Length == 0) return;
            string header = selected.Length == 1 ? selected[0].name : $"{selected.Length} objects selected";
            TagPickerWindow.OpenFor(header,
                (tagId, shared) =>
                {
                    foreach (var go in selected)
                        if (SceneNavigator.TryGetId(go, out var id, out _)) SceneNavigator.SetColor(id, tagId, shared);
                },
                () =>
                {
                    foreach (var go in selected)
                        if (SceneNavigator.TryGetId(go, out var id, out _)) SceneNavigator.ClearColor(id);
                });
        }

        [MenuItem(k_Root + "Color/Custom...", true)]
        static bool ColorCustomValidate() => AnySelected();

        [MenuItem(k_Root + "Color/None (block inheritance)", false, k_Priority + 61)]
        static void ColorNone(MenuCommand command)
        {
            if (First(command)) SceneNavigator.ColorSelection("", NavigatorUserState.instance.colorScopeShared);
        }

        [MenuItem(k_Root + "Color/None (block inheritance)", true)]
        static bool ColorNoneValidate() => AnySelected();

        [MenuItem(k_Root + "Color/Clear", false, k_Priority + 62)]
        static void ColorClear(MenuCommand command)
        {
            if (First(command)) SceneNavigator.ClearColorOnSelection();
        }

        [MenuItem(k_Root + "Color/Clear", true)]
        static bool ColorClearValidate() => AnySelected();
    }
}
