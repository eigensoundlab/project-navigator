// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using UnityEditor;

namespace ProjectNavigator
{
    /// <summary>
    /// Right-click entries in the Project window: Assets → Project Navigator → ...
    /// All entries work on folders and files, including multi-selections.
    /// Menu items must be static, so the Color sub-menu lists the built-in tags plus "Custom...",
    /// which opens a picker with every tag, including ones you created yourself.
    /// </summary>
    internal static class ContextMenus
    {
        const string k_Root = "Assets/Project Navigator/";
        const int k_Priority = 1200;

        static string[] SelectedGuids()
        {
            var guids = Selection.assetGUIDs;
            if (guids == null) return new string[0];
            return guids.Where(g => !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        }

        static bool AnySelected()
        {
            return SelectedGuids().Length > 0;
        }

        // ---------------------------------------------------------------- pins

        [MenuItem(k_Root + "Pin", false, k_Priority)]
        static void PinPersonal()
        {
            foreach (var guid in SelectedGuids()) Navigator.Pin(guid, false);
        }

        [MenuItem(k_Root + "Pin", true)]
        static bool PinPersonalValidate() => AnySelected();

        [MenuItem(k_Root + "Pin (Team)", false, k_Priority + 1)]
        static void PinShared()
        {
            foreach (var guid in SelectedGuids()) Navigator.Pin(guid, true);
        }

        [MenuItem(k_Root + "Pin (Team)", true)]
        static bool PinSharedValidate() => AnySelected();

        [MenuItem(k_Root + "Unpin", false, k_Priority + 2)]
        static void Unpin()
        {
            foreach (var guid in SelectedGuids()) Navigator.Unpin(guid);
        }

        [MenuItem(k_Root + "Unpin", true)]
        static bool UnpinValidate() => SelectedGuids().Any(Navigator.IsPinned);

        // ---------------------------------------------------------------- notes

        [MenuItem(k_Root + "Note...", false, k_Priority + 10)]
        static void SetNote()
        {
            var guids = SelectedGuids();
            if (guids.Length == 0) return;
            bool toShared = NavigatorUserState.instance.colorScopeShared;
            string initial = guids.Length == 1 ? Navigator.GetNote(guids[0]) : "";
            TextPromptWindow.Prompt("Note", guids.Length == 1 ? "Note" : $"Note for {guids.Length} assets", initial, value =>
            {
                foreach (var guid in guids) Navigator.SetNote(guid, value, toShared);
            });
        }

        [MenuItem(k_Root + "Note...", true)]
        static bool SetNoteValidate() => AnySelected();

        [MenuItem(k_Root + "Clear Note", false, k_Priority + 11)]
        static void ClearNote()
        {
            foreach (var guid in SelectedGuids()) Navigator.ClearNote(guid);
        }

        [MenuItem(k_Root + "Clear Note", true)]
        static bool ClearNoteValidate() => SelectedGuids().Any(g => !string.IsNullOrEmpty(Navigator.GetNote(g)));

        [MenuItem(k_Root + "Open Navigator Window", false, k_Priority + 20)]
        static void OpenWindow() => NavigatorWindow.Open();

        // ---------------------------------------------------------------- colors

        static void ApplyDefaultTag(string tagId)
        {
            var shared = NavigatorSharedSettings.instance;
            if (shared.EnsureDefaultTag(tagId) == null) return;
            bool toShared = NavigatorUserState.instance.colorScopeShared;
            foreach (var guid in SelectedGuids()) Navigator.SetColor(guid, tagId, toShared);
        }

        [MenuItem(k_Root + "Color/Yellow", false, k_Priority + 40)] static void ColorYellow() => ApplyDefaultTag(NavigatorSharedSettings.DefaultTags.Yellow);
        [MenuItem(k_Root + "Color/Yellow", true)] static bool ColorYellowValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Green", false, k_Priority + 41)] static void ColorGreen() => ApplyDefaultTag(NavigatorSharedSettings.DefaultTags.Green);
        [MenuItem(k_Root + "Color/Green", true)] static bool ColorGreenValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Blue", false, k_Priority + 42)] static void ColorBlue() => ApplyDefaultTag(NavigatorSharedSettings.DefaultTags.Blue);
        [MenuItem(k_Root + "Color/Blue", true)] static bool ColorBlueValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Orange", false, k_Priority + 43)] static void ColorOrange() => ApplyDefaultTag(NavigatorSharedSettings.DefaultTags.Orange);
        [MenuItem(k_Root + "Color/Orange", true)] static bool ColorOrangeValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Red", false, k_Priority + 44)] static void ColorRed() => ApplyDefaultTag(NavigatorSharedSettings.DefaultTags.Red);
        [MenuItem(k_Root + "Color/Red", true)] static bool ColorRedValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Purple", false, k_Priority + 45)] static void ColorPurple() => ApplyDefaultTag(NavigatorSharedSettings.DefaultTags.Purple);
        [MenuItem(k_Root + "Color/Purple", true)] static bool ColorPurpleValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Teal", false, k_Priority + 46)] static void ColorTeal() => ApplyDefaultTag(NavigatorSharedSettings.DefaultTags.Teal);
        [MenuItem(k_Root + "Color/Teal", true)] static bool ColorTealValidate() => AnySelected();
        [MenuItem(k_Root + "Color/Grey", false, k_Priority + 47)] static void ColorGrey() => ApplyDefaultTag(NavigatorSharedSettings.DefaultTags.Grey);
        [MenuItem(k_Root + "Color/Grey", true)] static bool ColorGreyValidate() => AnySelected();

        [MenuItem(k_Root + "Color/Custom...", false, k_Priority + 60)]
        static void ColorCustom() => TagPickerWindow.Open(SelectedGuids());

        [MenuItem(k_Root + "Color/Custom...", true)]
        static bool ColorCustomValidate() => AnySelected();

        [MenuItem(k_Root + "Color/None (block inheritance)", false, k_Priority + 61)]
        static void ColorNone()
        {
            bool toShared = NavigatorUserState.instance.colorScopeShared;
            foreach (var guid in SelectedGuids()) Navigator.SetColor(guid, "", toShared);
        }

        [MenuItem(k_Root + "Color/None (block inheritance)", true)]
        static bool ColorNoneValidate() => AnySelected();

        [MenuItem(k_Root + "Color/Clear (use rules and inheritance)", false, k_Priority + 62)]
        static void ColorClear()
        {
            foreach (var guid in SelectedGuids()) Navigator.ClearColor(guid);
        }

        [MenuItem(k_Root + "Color/Clear (use rules and inheritance)", true)]
        static bool ColorClearValidate() => AnySelected();
    }
}
