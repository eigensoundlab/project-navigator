// Copyright 2026 Daniel Sandner
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectNavigator
{
    /// <summary>
    /// Preferences → Project Navigator (personal settings) and
    /// Project Settings → Project Navigator (team: tags, rules, shared pins, colors and notes).
    /// </summary>
    internal static class NavigatorSettingsProviders
    {
        static Action s_Deferred;

        [SettingsProvider]
        static SettingsProvider CreatePreferences()
        {
            return new SettingsProvider("Preferences/Project Navigator", SettingsScope.User)
            {
                label = "Project Navigator",
                keywords = new HashSet<string>(new[] { "navigator", "folder", "history", "pin", "color", "frequent", "note", "floating" }),
                guiHandler = _ => DrawPreferences()
            };
        }

        [SettingsProvider]
        static SettingsProvider CreateProjectSettings()
        {
            return new SettingsProvider("Project/Project Navigator", SettingsScope.Project)
            {
                label = "Project Navigator",
                keywords = new HashSet<string>(new[] { "navigator", "folder", "color", "tag", "rule", "pin", "note" }),
                guiHandler = _ => DrawProjectSettings()
            };
        }

        // ---------------------------------------------------------------- preferences

        static void DrawPreferences()
        {
            var user = NavigatorUserState.instance;
            EditorGUIUtility.labelWidth = 300f;
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("History", EditorStyles.boldLabel);
            user.maxHistory = EditorGUILayout.IntSlider("History size (folders)", user.maxHistory, 20, 1000);
            user.halfLifeDays = EditorGUILayout.Slider("Frequency half-life (days)", user.halfLifeDays, 0.5f, 60f);
            user.recordParentFolderOfSelectedAssets = EditorGUILayout.Toggle("Record parent folder of selected assets", user.recordParentFolderOfSelectedAssets);
            user.trackFiles = EditorGUILayout.Toggle(new GUIContent("Track selected files (\"Recent files\")", "Remembers files you select. Folder back/forward is not affected."), user.trackFiles);
            using (new EditorGUI.DisabledScope(!user.trackFiles))
                user.recentFilesCount = EditorGUILayout.IntSlider("Recent files shown", user.recentFilesCount, 3, 50);
            user.ignorePackages = EditorGUILayout.Toggle("Ignore assets under Packages/", user.ignorePackages);
            user.pollIntervalSeconds = EditorGUILayout.Slider("Active-folder poll interval (seconds)", user.pollIntervalSeconds, 0.1f, 2f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Project window and Inspector", EditorStyles.boldLabel);
            user.overlayEnabled = EditorGUILayout.Toggle("Draw colors in the Project window", user.overlayEnabled);
            user.showPinBadge = EditorGUILayout.Toggle("Show a badge on pinned assets", user.showPinBadge);
            user.tintFilesInColoredFolders = EditorGUILayout.Toggle("Tint files inside colored folders (per tag)", user.tintFilesInColoredFolders);
            user.showNotesInProjectWindow = EditorGUILayout.Toggle("Show notes in Project window list rows", user.showNotesInProjectWindow);
            user.inspectorStripEnabled = EditorGUILayout.Toggle("Show color/pin/note strip in the folder Inspector", user.inspectorStripEnabled);
            using (new EditorGUI.DisabledScope(!user.inspectorStripEnabled))
                user.inspectorStripForFiles = EditorGUILayout.Toggle("Also show the strip for files", user.inspectorStripForFiles);
            user.colorScopeShared = EditorGUILayout.Toggle("New colors and notes are team-shared (ProjectSettings)", user.colorScopeShared);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Navigator window", EditorStyles.boldLabel);
            user.folderClickAction = (FolderClickAction)EditorGUILayout.EnumPopup(new GUIContent("Folder rows", "What a click on a folder row does. Files always: click = select, double-click = open."), user.folderClickAction);
            user.frequentCount = EditorGUILayout.IntSlider("Frequent folders shown", user.frequentCount, 3, 50);
            user.recentCount = EditorGUILayout.IntSlider("Recent folders shown", user.recentCount, 3, 100);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Floating window", EditorStyles.boldLabel);
            user.floatingOnTop = EditorGUILayout.Toggle(new GUIContent("Stay on top (utility window)", "Keeps the floating Navigator above the main editor window and everything docked in it. Takes effect the next time the floating window is opened."), user.floatingOnTop);
            user.reopenFloatingOnStartup = EditorGUILayout.Toggle("Reopen the floating window on startup", user.reopenFloatingOnStartup);

            if (EditorGUI.EndChangeCheck())
                Commit(user: true);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear history", GUILayout.Width(130f)) &&
                    EditorUtility.DisplayDialog("Project Navigator", "Clear the folder/file history and frequency data?", "Clear", "Cancel"))
                    Navigator.ClearHistory();
                if (GUILayout.Button("Remove missing entries", GUILayout.Width(160f)))
                    Navigator.PruneMissing();
                if (GUILayout.Button("Save now", GUILayout.Width(90f)))
                    Navigator.FlushSaves();
                if (GUILayout.Button("Open floating window", GUILayout.Width(150f)))
                    NavigatorWindow.OpenFloating();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Recover from legacy .asset files", "Merges pins, colors, notes, tags and rules saved by versions up to 0.3.0 (ProjectNavigator.asset) into the current data."), GUILayout.Width(220f)))
                    EditorUtility.DisplayDialog("Project Navigator", Navigator.RecoverFromLegacyAssets(), "OK");
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Personal data: UserSettings/ProjectNavigator.json (keep it out of version control).\n" +
                "Team data: ProjectSettings/ProjectNavigator.json (commit it).\n" +
                "Folder navigation: " + (ProjectBrowserBridge.HasInternalsForNavigation ? "editor internals (optional file present)" : "public API (Asset Store compliant)") + ".\n" +
                "Browsed-folder tracking: " + (ProjectBrowserBridge.CanReadActiveFolder ? "available" : "selection-based only (no internals file)") + ".",
                MessageType.None);
            DrawFooter();
        }

        static void DrawFooter()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"Project Navigator {NavigatorVersion.Current}  ·  Apache-2.0  ·  Daniel Sandner", EditorStyles.miniLabel);
                if (GUILayout.Button("GitHub", EditorStyles.miniButton, GUILayout.Width(52f)))
                    Application.OpenURL(NavigatorVersion.Repository);
                GUILayout.FlexibleSpace();
            }
        }

        // ---------------------------------------------------------------- project settings

        static void DrawProjectSettings()
        {
            var shared = NavigatorSharedSettings.instance;
            var tagNames = TagNames(shared);
            EditorGUIUtility.labelWidth = 120f;
            EditorGUI.BeginChangeCheck();

            // ---- tags
            EditorGUILayout.LabelField("Color tags", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("A tag is a named color. Assign tags to folders or files from the Project window (right-click → Project Navigator → Color), from the Inspector strip or from the Navigator window. Changing a tag's color recolors every asset using it.", MessageType.None);
            for (int i = 0; i < shared.tags.Count; i++)
            {
                var tag = shared.tags[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    tag.name = EditorGUILayout.TextField(tag.name, GUILayout.MinWidth(80f));
                    tag.color = EditorGUILayout.ColorField(GUIContent.none, tag.color, true, false, false, GUILayout.Width(60f));
                    tag.inheritToChildren = GUILayout.Toggle(tag.inheritToChildren, new GUIContent("Inherit", "Sub-folders without their own color take this color"), GUILayout.Width(66f));
                    tag.tintFiles = GUILayout.Toggle(tag.tintFiles, new GUIContent("Files", "Draw a colored bar on files inside folders with this color"), GUILayout.Width(54f));
                    int uses = CountUses(shared, tag.id);
                    GUILayout.Label(uses + (uses == 1 ? " asset" : " assets"), EditorStyles.miniLabel, GUILayout.Width(66f));
                    if (GUILayout.Button("X", GUILayout.Width(22f)) &&
                        EditorUtility.DisplayDialog("Delete tag", $"Delete tag \"{tag.name}\"? Assets using it lose their color.", "Delete", "Cancel"))
                    {
                        var id = tag.id;
                        s_Deferred = () => DeleteTag(shared, id);
                    }
                }
            }
            if (GUILayout.Button("Add tag", GUILayout.Width(100f)))
                shared.tags.Add(new ColorTag { name = "Tag " + (shared.tags.Count + 1) });

            // ---- rules
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rules (automatic folder colors by path)", EditorStyles.boldLabel);
            shared.rulesEnabled = EditorGUILayout.ToggleLeft("Enable rules", shared.rulesEnabled);
            EditorGUILayout.HelpBox("Glob patterns matched against the folder path: * = anything except '/', ** = anything, **/Resources = any folder named Resources. Precedence: explicit color on the folder > matching rule > color inherited from a parent.", MessageType.None);
            using (new EditorGUI.DisabledScope(!shared.rulesEnabled))
            {
                for (int i = 0; i < shared.rules.Count; i++)
                {
                    var rule = shared.rules[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        rule.enabled = GUILayout.Toggle(rule.enabled, GUIContent.none, GUILayout.Width(18f));
                        rule.pattern = EditorGUILayout.TextField(rule.pattern, GUILayout.MinWidth(120f));
                        int next = EditorGUILayout.Popup(IndexOfTag(shared, rule.tagId) + 1, tagNames, GUILayout.Width(110f)) - 1;
                        rule.tagId = next >= 0 ? shared.tags[next].id : "";
                        if (GUILayout.Button("X", GUILayout.Width(22f)))
                        {
                            var target = rule;
                            s_Deferred = () => shared.rules.Remove(target);
                        }
                    }
                }
                if (GUILayout.Button("Add rule", GUILayout.Width(100f)))
                    shared.rules.Add(new ColorRule { pattern = "Assets/**", tagId = shared.tags.Count > 0 ? shared.tags[0].id : "" });
            }

            // ---- colored assets
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Colored folders and files (team)", EditorStyles.boldLabel);
            if (shared.folderColors.Count == 0)
                EditorGUILayout.LabelField("None yet.", EditorStyles.miniLabel);
            for (int i = 0; i < shared.folderColors.Count; i++)
            {
                var entry = shared.folderColors[i];
                var info = Navigator.GetInfo(entry.guid);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var tag = shared.FindTag(entry.tagId);
                    var swatch = GUILayoutUtility.GetRect(12f, 12f, GUILayout.Width(12f), GUILayout.Height(12f));
                    swatch.y += 3f;
                    EditorGUI.DrawRect(swatch, tag != null ? tag.color : new Color(0.5f, 0.5f, 0.5f, 0.3f));
                    EditorGUILayout.LabelField(info.Exists ? info.path : "(missing) " + entry.guid, GUILayout.MinWidth(100f));
                    int next = EditorGUILayout.Popup(IndexOfTag(shared, entry.tagId) + 1, tagNames, GUILayout.Width(110f)) - 1;
                    entry.tagId = next >= 0 ? shared.tags[next].id : "";
                    if (GUILayout.Button("X", GUILayout.Width(22f)))
                    {
                        var target = entry;
                        s_Deferred = () => shared.folderColors.Remove(target);
                    }
                }
            }

            // ---- shared pins
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Pinned (team)", EditorStyles.boldLabel);
            if (shared.pins.Count == 0)
                EditorGUILayout.LabelField("None yet. Right-click an asset → Project Navigator → Pin (Team).", EditorStyles.miniLabel);
            for (int i = 0; i < shared.pins.Count; i++)
            {
                var pin = shared.pins[i];
                var info = Navigator.GetInfo(pin.guid);
                using (new EditorGUILayout.HorizontalScope())
                {
                    pin.label = EditorGUILayout.TextField(pin.label, GUILayout.Width(120f));
                    pin.group = EditorGUILayout.TextField(pin.group, GUILayout.Width(90f));
                    EditorGUILayout.LabelField(info.Exists ? info.path : "(missing) " + pin.guid);
                    if (GUILayout.Button("X", GUILayout.Width(22f)))
                    {
                        var target = pin;
                        s_Deferred = () => shared.pins.Remove(target);
                    }
                }
            }

            // ---- shared notes
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Notes (team)", EditorStyles.boldLabel);
            if (shared.notes.Count == 0)
                EditorGUILayout.LabelField("None yet. Right-click an asset → Project Navigator → Note...", EditorStyles.miniLabel);
            for (int i = 0; i < shared.notes.Count; i++)
            {
                var note = shared.notes[i];
                var info = Navigator.GetInfo(note.guid);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(info.Exists ? info.path : "(missing) " + note.guid, GUILayout.MinWidth(100f));
                    note.text = EditorGUILayout.TextField(note.text, GUILayout.MinWidth(120f));
                    if (GUILayout.Button("X", GUILayout.Width(22f)))
                    {
                        var target = note;
                        s_Deferred = () => shared.notes.Remove(target);
                    }
                }
            }

            bool changed = EditorGUI.EndChangeCheck();

            // Deletions run after the GUI pass so the layout stays consistent within one frame.
            if (s_Deferred != null)
            {
                var action = s_Deferred;
                s_Deferred = null;
                action();
                changed = true;
            }
            if (changed) Commit(shared: true);

            // ---- maintenance
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export JSON...", GUILayout.Width(110f))) Export(shared);
                if (GUILayout.Button("Import JSON...", GUILayout.Width(110f))) Import(shared);
                if (GUILayout.Button("Remove missing", GUILayout.Width(110f)))
                    Debug.Log($"[Project Navigator] Removed {Navigator.PruneMissingShared()} entries that pointed to missing assets.");
                if (GUILayout.Button("Reset to defaults", GUILayout.Width(120f)) &&
                    EditorUtility.DisplayDialog("Project Navigator", "Reset tags, rules, team pins, team colors and team notes to the defaults?", "Reset", "Cancel"))
                {
                    shared.ResetToDefaults();
                    Commit(shared: true);
                }
            }
            EditorGUILayout.HelpBox("Stored in ProjectSettings/ProjectNavigator.json. Commit that file to share tags, rules, pins, colors and notes with the team. Personal overrides live in each user's UserSettings folder.", MessageType.None);
            DrawFooter();
        }

        // ---------------------------------------------------------------- helpers

        static void Commit(bool user = false, bool shared = false)
        {
            Navigator.MarkDirty(user: user, shared: shared);
            FolderColorResolver.Invalidate();
            Navigator.NotifyChanged();
        }

        static string[] TagNames(NavigatorSharedSettings shared)
        {
            var names = new string[shared.tags.Count + 1];
            names[0] = "None";
            for (int i = 0; i < shared.tags.Count; i++)
                names[i + 1] = string.IsNullOrEmpty(shared.tags[i].name) ? "(unnamed)" : shared.tags[i].name;
            return names;
        }

        static int IndexOfTag(NavigatorSharedSettings shared, string tagId)
        {
            return string.IsNullOrEmpty(tagId) ? -1 : shared.tags.FindIndex(t => t.id == tagId);
        }

        static int CountUses(NavigatorSharedSettings shared, string tagId)
        {
            return shared.folderColors.Count(f => f.tagId == tagId) + NavigatorUserState.instance.folderColors.Count(f => f.tagId == tagId);
        }

        static void DeleteTag(NavigatorSharedSettings shared, string tagId)
        {
            shared.tags.RemoveAll(t => t.id == tagId);
            shared.folderColors.RemoveAll(f => f.tagId == tagId);
            NavigatorUserState.instance.folderColors.RemoveAll(f => f.tagId == tagId);
            foreach (var rule in shared.rules)
                if (rule.tagId == tagId) rule.tagId = "";
            Navigator.MarkDirty(user: true);
        }

        static void Export(NavigatorSharedSettings shared)
        {
            var path = EditorUtility.SaveFilePanel("Export Project Navigator settings", "", "ProjectNavigator.json", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, EditorJsonUtility.ToJson(shared, true));
        }

        static void Import(NavigatorSharedSettings shared)
        {
            var path = EditorUtility.OpenFilePanel("Import Project Navigator settings", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(path), shared);
            Commit(shared: true);
        }
    }
}
