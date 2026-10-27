// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Global, rebindable shortcuts (Edit → Shortcuts → search "Project Navigator").
    /// Defaults stay clear of Unity 6.6's native selection history (Ctrl+Shift+[ / ]) and use
    /// Alt + arrow keys, which behave the same on every keyboard layout.
    /// </summary>
    internal static class NavigatorShortcuts
    {
        [Shortcut("Project Navigator/Back", KeyCode.LeftArrow, ShortcutModifiers.Alt)]
        static void Back() => Navigator.Back();

        [Shortcut("Project Navigator/Forward", KeyCode.RightArrow, ShortcutModifiers.Alt)]
        static void Forward() => Navigator.Forward();

        [Shortcut("Project Navigator/Parent Folder", KeyCode.UpArrow, ShortcutModifiers.Alt)]
        static void Parent() => Navigator.GoToParent();

        [Shortcut("Project Navigator/Open Window", KeyCode.N, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        static void OpenWindow() => NavigatorWindow.Open();

        [Shortcut("Project Navigator/Quick Jump", KeyCode.F, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        static void QuickJump() => NavigatorSearchProvider.ShowQuickJump();

        [Shortcut("Project Navigator/Popup at Cursor", KeyCode.Space, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        static void Popup() => NavigatorPopup.Toggle();

        [Shortcut("Project Navigator/Pin Current Folder", KeyCode.P, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        static void PinCurrent() => Navigator.PinCurrentFolder(false);

        [Shortcut("Project Navigator/Pin Slot 1", KeyCode.Alpha1, ShortcutModifiers.Alt)] static void Slot1() => Navigator.OpenPinSlot(0);
        [Shortcut("Project Navigator/Pin Slot 2", KeyCode.Alpha2, ShortcutModifiers.Alt)] static void Slot2() => Navigator.OpenPinSlot(1);
        [Shortcut("Project Navigator/Pin Slot 3", KeyCode.Alpha3, ShortcutModifiers.Alt)] static void Slot3() => Navigator.OpenPinSlot(2);
        [Shortcut("Project Navigator/Pin Slot 4", KeyCode.Alpha4, ShortcutModifiers.Alt)] static void Slot4() => Navigator.OpenPinSlot(3);
        [Shortcut("Project Navigator/Pin Slot 5", KeyCode.Alpha5, ShortcutModifiers.Alt)] static void Slot5() => Navigator.OpenPinSlot(4);
        [Shortcut("Project Navigator/Pin Slot 6", KeyCode.Alpha6, ShortcutModifiers.Alt)] static void Slot6() => Navigator.OpenPinSlot(5);
        [Shortcut("Project Navigator/Pin Slot 7", KeyCode.Alpha7, ShortcutModifiers.Alt)] static void Slot7() => Navigator.OpenPinSlot(6);
        [Shortcut("Project Navigator/Pin Slot 8", KeyCode.Alpha8, ShortcutModifiers.Alt)] static void Slot8() => Navigator.OpenPinSlot(7);
        [Shortcut("Project Navigator/Pin Slot 9", KeyCode.Alpha9, ShortcutModifiers.Alt)] static void Slot9() => Navigator.OpenPinSlot(8);
    }
}
