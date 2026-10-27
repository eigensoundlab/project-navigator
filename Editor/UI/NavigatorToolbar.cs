// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

#if UNITY_6000_4_OR_NEWER
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// The "always visible, minimized" form of the tool: back/forward buttons and a Folders dropdown
    /// in the Editor's main toolbar (public API since Unity 6.4). Users can hide or move the element
    /// from the toolbar's own context menu, so it never gets in the way.
    /// </summary>
    internal static class NavigatorToolbar
    {
        public const string Id = "Project Navigator/Folders";

        [MainToolbarElement(Id, defaultDockPosition = MainToolbarDockPosition.Middle)]
        static IEnumerable<MainToolbarElement> Create()
        {
            yield return new MainToolbarButton(new MainToolbarContent("<", "Project Navigator: previous folder (Alt+Left)"), Navigator.Back);
            yield return new MainToolbarButton(new MainToolbarContent(">", "Project Navigator: next folder (Alt+Right)"), Navigator.Forward);
            yield return new MainToolbarDropdown(new MainToolbarContent("Folders", "Pinned, frequent and recent folders"), ShowMenu);
        }

        static void ShowMenu(Rect dropdownRect)
        {
            var user = NavigatorUserState.instance;
            var menu = new GenericMenu();
            bool any = false;

            foreach (var (pin, shared) in Navigator.AllPins())
            {
                var info = Navigator.GetInfo(pin.guid);
                if (!info.Exists) continue;
                var label = string.IsNullOrEmpty(pin.label) ? info.name : pin.label;
                var group = string.IsNullOrEmpty(pin.group) ? "" : Sanitize(pin.group) + "/";
                var guid = pin.guid;
                menu.AddItem(new GUIContent("Pinned/" + group + Sanitize(label) + WithNote(guid) + (shared ? "  (team)" : "")), false, () => Navigator.Open(guid));
                any = true;
            }

            foreach (var (info, _) in Navigator.Frequent(8))
            {
                var guid = info.guid;
                menu.AddItem(new GUIContent("Frequent/" + Describe(info)), false, () => Navigator.Open(guid));
                any = true;
            }

            foreach (var info in Navigator.Recent(10))
            {
                var guid = info.guid;
                menu.AddItem(new GUIContent("Recent/" + Describe(info)), false, () => Navigator.Open(guid));
                any = true;
            }

            if (user.trackFiles)
            {
                foreach (var info in Navigator.RecentFiles(user.recentFilesCount))
                {
                    var guid = info.guid;
                    menu.AddItem(new GUIContent("Recent files/" + Describe(info)), false, () => Navigator.Open(guid));
                    any = true;
                }
            }

            if (any) menu.AddSeparator("");
            menu.AddItem(new GUIContent("Pin current folder"), false, () => Navigator.PinCurrentFolder(false));
            menu.AddItem(new GUIContent("Popup at cursor (Alt+Shift+Space)"), false, NavigatorPopup.Open);
            menu.AddItem(new GUIContent("Quick jump (search)"), false, NavigatorSearchProvider.ShowQuickJump);
            menu.AddItem(new GUIContent("Open Navigator window"), false, NavigatorWindow.Open);
            menu.AddItem(new GUIContent("Open floating Navigator"), false, NavigatorWindow.OpenFloating);
            menu.DropDown(dropdownRect);
        }

        static string Describe(AssetInfo info)
        {
            var parent = Navigator.NameOf(Navigator.ParentOf(info.path));
            return Sanitize(info.name) + (string.IsNullOrEmpty(parent) ? "" : "   (" + Sanitize(parent) + ")") + WithNote(info.guid);
        }

        static string WithNote(string guid)
        {
            var note = Navigator.GetNote(guid);
            if (string.IsNullOrEmpty(note)) return "";
            if (note.Length > 40) note = note.Substring(0, 38) + "…";
            return "  — " + Sanitize(note);
        }

        // A '/' inside a label would create a sub-menu in GenericMenu.
        static string Sanitize(string text) => text.Replace('/', '∕');
    }
}
#endif
