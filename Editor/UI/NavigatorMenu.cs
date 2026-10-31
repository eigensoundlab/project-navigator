// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using UnityEditor;

namespace ProjectNavigator
{
    /// <summary>Window → Project Navigator entries that do not belong to a specific window class.</summary>
    internal static class NavigatorMenu
    {
        [MenuItem("Window/Project Navigator/Popup at Cursor", false, 12)]
        static void Popup() => NavigatorPopup.Open(NavigatorPopup.Mode.Navigator);

        [MenuItem("Window/Project Navigator/Window Switcher", false, 14)]
        static void WindowSwitcher() => NavigatorPopup.Open(NavigatorPopup.Mode.Windows);

        [MenuItem("Window/Project Navigator/Pin a Tool...", false, 15)]
        static void PinTool() => ToolPickerWindow.Open();

        [MenuItem("Window/Project Navigator/Pin Selected GameObjects", false, 16)]
        static void PinSelectedGameObjects() => SceneNavigator.PinSelection(false);

        [MenuItem("Window/Project Navigator/Quick Jump (Search)", false, 13)]
        static void QuickJump() => NavigatorSearchProvider.ShowQuickJump();

        [MenuItem("Window/Project Navigator/Preferences...", false, 30)]
        static void Preferences() => SettingsService.OpenUserPreferences("Preferences/Project Navigator");

        [MenuItem("Window/Project Navigator/Team Settings (Tags, Rules)...", false, 31)]
        static void ProjectSettings() => SettingsService.OpenProjectSettings("Project/Project Navigator");
    }
}
