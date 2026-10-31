// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Scene view overlay "Project Navigator": pin / color / note the selection, toggle isolation, and jump
    /// to pinned objects without leaving the Scene view. Hidden by default; enable it from the Scene view's
    /// Overlays menu (the ⋮ button or the Overlays shortcut).
    /// </summary>
    [Overlay(typeof(SceneView), k_Id, "Project Navigator", false)]
    internal sealed class NavigatorSceneOverlay : ToolbarOverlay
    {
        public const string k_Id = "ProjectNavigator/SceneOverlay";

        public NavigatorSceneOverlay() : base(PinButton.k_Id, ColorDropdown.k_Id, NoteButton.k_Id, IsolateButton.k_Id, PinsDropdown.k_Id)
        {
        }
    }

    [EditorToolbarElement(k_Id, typeof(SceneView))]
    internal sealed class PinButton : EditorToolbarButton
    {
        public const string k_Id = "ProjectNavigator/Scene/Pin";

        public PinButton()
        {
            text = "Pin";
            tooltip = "Pin the selected GameObjects in Project Navigator (Alt+Shift+G)";
            clicked += () => SceneNavigator.PinSelection(false);
        }
    }

    [EditorToolbarElement(k_Id, typeof(SceneView))]
    internal sealed class ColorDropdown : EditorToolbarDropdown
    {
        public const string k_Id = "ProjectNavigator/Scene/Color";

        public ColorDropdown()
        {
            text = "Color";
            tooltip = "Color the selected GameObjects";
            clicked += ShowMenu;
        }

        void ShowMenu()
        {
            var shared = NavigatorSharedSettings.instance;
            bool toShared = NavigatorUserState.instance.colorScopeShared;
            var menu = new GenericMenu();
            if (Selection.gameObjects.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("Select a GameObject first"));
            }
            else
            {
                foreach (var tag in shared.tags)
                {
                    var tagId = tag.id;
                    menu.AddItem(new GUIContent(tag.name.Replace('/', '∕')), false, () => SceneNavigator.ColorSelection(tagId, toShared));
                }
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("None (block inheritance)"), false, () => SceneNavigator.ColorSelection("", toShared));
                menu.AddItem(new GUIContent("Clear"), false, SceneNavigator.ClearColorOnSelection);
            }
            menu.DropDown(worldBound);
        }
    }

    [EditorToolbarElement(k_Id, typeof(SceneView))]
    internal sealed class NoteButton : EditorToolbarButton
    {
        public const string k_Id = "ProjectNavigator/Scene/Note";

        public NoteButton()
        {
            text = "Note";
            tooltip = "Add or edit the note of the selected GameObject";
            clicked += () =>
            {
                var selected = Selection.gameObjects;
                if (selected.Length == 0) return;
                bool toShared = NavigatorUserState.instance.colorScopeShared;
                string initial = selected.Length == 1 && SceneNavigator.TryGetId(selected[0], out var firstId, out _) ? SceneNavigator.GetNote(firstId) : "";
                TextPromptWindow.Prompt("Note", selected.Length == 1 ? "Note" : $"Note for {selected.Length} objects", initial, value =>
                {
                    foreach (var go in selected)
                        if (SceneNavigator.TryGetId(go, out var id, out _)) SceneNavigator.SetNote(id, value, toShared);
                });
            };
        }
    }

    [EditorToolbarElement(k_Id, typeof(SceneView))]
    internal sealed class IsolateButton : EditorToolbarButton
    {
        public const string k_Id = "ProjectNavigator/Scene/Isolate";

        public IsolateButton()
        {
            text = "Isolate";
            tooltip = "Isolate the selection in the Scene view, or exit isolation (Unity's Shift+H)";
            clicked += () =>
            {
                if (SceneNavigator.IsIsolated) SceneNavigator.ExitIsolation();
                else SceneNavigator.IsolateSelection();
            };
        }
    }

    [EditorToolbarElement(k_Id, typeof(SceneView))]
    internal sealed class PinsDropdown : EditorToolbarDropdown
    {
        public const string k_Id = "ProjectNavigator/Scene/Pins";

        public PinsDropdown()
        {
            text = "Pins";
            tooltip = "Jump to a pinned scene object (selects and frames it)";
            clicked += ShowMenu;
        }

        void ShowMenu()
        {
            var menu = new GenericMenu();
            bool any = false;
            foreach (var (pin, shared) in SceneNavigator.AllPins())
            {
                var info = SceneNavigator.GetInfo(pin.id, pin.lastName);
                var label = (string.IsNullOrEmpty(pin.label) ? info.name : pin.label).Replace('/', '∕');
                var id = pin.id;
                bool loaded = info.Exists;
                menu.AddItem(new GUIContent(info.sceneName.Replace('/', '∕') + "/" + label + (loaded ? "" : "  (scene not loaded)") + (shared ? "  (team)" : "")),
                    false, () => { if (loaded) SceneNavigator.Frame(id); else SceneNavigator.OpenSceneAndSelect(id, false); });
                any = true;
            }
            if (!any) menu.AddDisabledItem(new GUIContent("No pinned scene objects"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Open Navigator window"), false, NavigatorWindow.Open);
            menu.DropDown(worldBound);
        }
    }
}
