# Project Navigator — developer notes

Technical background for contributors. User instructions are in the root README.md.

Pinned, frequent and recent folders *and files*, colors and notes for folders and files,
back/forward navigation, a floating "stay on top" panel and a quick-jump search for the Project
window. Editor-only; nothing is written into `Assets/` or `.meta` files.

Status: v0.3.0. v0.1 and v0.2 were confirmed to compile and run on 6000.6.

## Install

Option A (recommended): copy the folder `com.yourstudio.project-navigator` into your project's
`Packages/` directory. Unity picks it up as an embedded package.

Option B: Window → Package Manager → `+` → *Add package from disk...* → pick `package.json`.

Option C: copy the `Editor/` folder into `Assets/ProjectNavigator/`. Everything works the same;
the test assembly in `Tests/` is optional.

Rename `name` in `package.json` and fill in `LICENSE.md` before publishing.

## What you get

| Where | What |
| --- | --- |
| Window → General → **Project Navigator** | Dockable window: Pinned / Frequent / Recent / Recent files / Colored. Click a folder row to open it; click a file row to select it, double-click to open it. Right-click for actions. Drop folders or files onto the window to pin them; drop assets onto a folder row to move them into that folder. *Compact* hides everything but the pins. |
| **Float** button, or Window → General → Project Navigator (Floating) | A second, independent copy as a floating panel. With *Stay on top* enabled (Preferences, default on) it is a utility window: always above the main editor window and everything docked in it. It remembers its position and compact state and is reopened on startup (Unity does not restore utility windows by itself). |
| Main toolbar (Unity 6.4+) | `<` `>` buttons and a **Folders** dropdown: pinned, frequent, recent folders and (optionally) recent files, with notes. Hide or move it from the toolbar's own right-click menu. |
| Right-click a folder or file → **Project Navigator** | Pin (personal / team), Unpin, Note..., Clear Note, Color → tag / Custom... / None / Clear. Works on multi-selections. |
| Inspector | A strip under the header for folders (and, optionally, files): color swatch, tag dropdown, Pin, Open, Note. |
| Project window | Colored folder icons, a colored bar on colored files, a dot on pinned assets, and the note right-aligned in list rows (only for assets that have one). |
| Ctrl+K, type `nav:` (or Alt+Shift+F) | Fuzzy search over every folder plus pinned and recent files, matching name, path and note, ranked by pins and frequency. |
| Preferences → Project Navigator | Personal settings. |
| Project Settings → Project Navigator | Team settings: tags, rules, shared pins, colors and notes, JSON export/import. |

## Shortcuts (Edit → Shortcuts → "Project Navigator", all rebindable)

| Default | Action |
| --- | --- |
| Alt+Left / Alt+Right | Back / Forward through folders you browsed |
| Alt+Up | Parent folder |
| Alt+Shift+N | Open the Navigator window |
| Alt+Shift+F | Quick jump (Search window scoped to `nav:`) |
| Alt+Shift+P | Pin / unpin the current folder |
| Alt+1 ... Alt+9 | Open pin slot 1-9 (team pins first, then personal, in list order) |

Unity 6.6's own selection history stays on Ctrl+Shift+[ and ]; this tool does not touch it.

## Files, labels, notes and renaming

- **Files** can be pinned, colored and annotated exactly like folders. Opening a pinned file selects
  and pings it; double-click (or *Open Asset*) opens it the way the Project window would (scene,
  prefab stage, script in the IDE, external app).
- **Recent files** is opt-in (Preferences → *Track selected files*). It is a separate list; the
  folder back/forward history never contains files.
- **Pin label** changes only the text shown in the Navigator (the menu entry says "display only").
  The asset on disk keeps its name.
- **Rename on disk...** (row context menu) is the one action that renames the real asset. It asks
  for confirmation first; references survive because Unity keeps them by GUID.
- **Notes** are short texts attached to a folder or file, meant to disambiguate same-named folders
  ("Textures — enemy variants", "Levels — campaign only"). They show in the Navigator rows, in the
  toolbar dropdown, in the Inspector strip, in search results and, for list rows, right-aligned in
  the Project window. Notes follow the same team/personal scope switch as colors.

## How colors work

- A **tag** is a named color (Yellow, Green, ... or your own). Assets reference tags, so changing
  a tag's color recolors everything that uses it.
- Precedence for a folder: **explicit color on the folder** (a personal override beats the team
  value) → **first enabled rule** whose glob matches the folder path → **nearest ancestor** with
  an explicit color whose tag has *Inherit* on.
- A file: its own explicit color if it has one (drawn as a full-strength bar); otherwise its
  folder's tag when that tag has *Files* enabled (a lighter bar).
- *None* on a folder means "no color, and nothing inherits through me". *Clear* removes the
  explicit entry so rules and inheritance apply again.
- Rules are off by default. Turn them on in Project Settings; the defaults are
  `**/Resources` = Orange, `**/Editor` = Grey, `**/StreamingAssets` = Purple, `**/Plugins` = Grey,
  `**/Scenes` = Green. Glob syntax: `*` anything but `/`, `**` anything, `**/Name` any depth.

## Where data lives

| File | Scope | Commit? |
| --- | --- | --- |
| `ProjectSettings/ProjectNavigator.asset` | tags, rules, team pins, team colors, team notes | yes |
| `UserSettings/ProjectNavigator.asset` | history, frequency scores, personal pins/colors/notes, preferences, floating-window state | no (Unity's default .gitignore excludes `UserSettings/`) |

Everything references assets by **GUID**, so renames and moves survive. Deleted assets are pruned
from personal data automatically; team data has a *Remove missing* button instead, so one person's
missing asset never deletes a teammate's pin. v0.1 data files load unchanged.

## What to test in v0.2

1. **Compile** (most files changed). Console first.
2. **Files:** pin a ScriptableObject and a scene; click (select+ping) and double-click (open); color a file and check the bar in list and grid views; drag a pinned file row into an Object field.
3. **Recent files:** enable *Track selected files*, select a few assets, check the section and the toolbar dropdown.
4. **Notes:** add a note to two same-named folders; check the Navigator rows, the Project window list rows (right-aligned, dimmed), the Inspector strip and `nav:` search by note text.
5. **Floating window:** press *Float*. It should stay above the Scene/Game/Project views while you work; close Unity, reopen, it should come back at the same place. Toggle *Stay on top* off, reopen the float, it becomes a normal floating window.
6. **Rename on disk** on a test folder: confirmation dialog, folder renamed, pin/color/note still attached (GUID).
7. **Regression:** everything from the v0.1 checklist (two-column open, overlay alignment, shortcuts, persistence).

## Known limits

- "Stay on top" is Unity's utility-window behavior: above the main editor window and docked views, not above other floating windows or other applications. No API exists for more.
- Pin reordering is via right-click → Move up / Move down.
- Only main assets are handled (no sub-assets such as sprites inside a texture).
- The left Project tree shows an "opened folder" icon on expanded folders; the tint draws the closed shape over it. Cosmetic.
- Undo is not wired for pin/color/note changes.

## Layout

```
Editor/
  Core/      models, HistoryStack, Frecency, FuzzyMatcher, GlobMatcher, settings singletons,
             Navigator (facade), ProjectBrowserBridge (the only reflection), FolderTracker, asset watcher
  Coloring/  FolderColorResolver (precedence + cache), ProjectWindowOverlay, InspectorHeaderStrip
  UI/        NavigatorWindow (UI Toolkit) + .uss, context menus, shortcuts, main-toolbar element,
             settings providers, tag picker, text prompt
  Search/    "nav:" search provider
Tests/Editor/ edit-mode tests for the pure logic
```

## Folder navigation without reflection (v0.3)

Unity's own double-click on a folder runs `ProjectBrowser.OpenSelectedFolders()`, which acts on the
folders currently selected in the browser's list area. Native `AssetDatabase.OpenAsset` on a folder
calls back into that method (`OpenSelectedFoldersInInternalExplorer`, marked "Called from
EditorHelper" in UnityCsReference). So the public sequence is: `EditorUtility.FocusProjectWindow()`,
`Selection.activeObject = folder` (the browser frames the folder in its parent and selects it in the
list), then `AssetDatabase.OpenAsset(folder)` on the next editor tick. Two-column: shows the folder's
contents; one-column: frames and expands it in the tree.

`ProjectBrowserBridge.cs` is a `static partial class` with `static partial void` hooks. When
`ProjectBrowserBridge.Internals.cs` is present (GitHub flavor), the hooks are implemented with
reflection (`ProjectBrowser.ShowFolderContents(EntityId, bool)`, `ProjectWindowUtil.TryGetActiveFolderPath`).
When the file is absent (Asset Store flavor, see `Tools~/make-store-build.sh`), the calls compile
to nothing and the public path is used. `PROJECT_NAVIGATOR_NO_INTERNALS` disables the file without deleting it.

## Asset Store checklist (submission guidelines, Oct 2026)

- 2.5.g no reflection into editor internals → remove the Internals file.
- 2.5.h support Play mode with domain reload disabled → all static state is initialized idempotently.
- 2.5.i no obsolete-API warnings → `GetEntityId` under `UNITY_6000_3_OR_NEWER`.
- 2.5.1.a menus under `Window/<PackageName>` → `Window/Project Navigator/...`.
- 1.2.a Third-Party Notices file → present (no third-party components).
- 2.3 documentation → README.md (+ this file).
- 1.2.b no attribution-requiring dependent licenses → applies to third-party code only; the author's
  own code is offered to the store under the Asset Store EULA. If outside contributions are merged,
  keep store releases to author-owned code or get contributors' explicit permission (see CONTRIBUTING.md).

## Popup at cursor (v0.3)

`NavigatorPopup` is an `EditorWindow` shown with `ShowAsDropDown(anchorRect, size)`: Unity keeps it on
screen and closes it when it loses focus, so stacking against other floating windows is irrelevant.
The anchor is `GUIUtility.GUIToScreenPoint(Event.current.mousePosition)` when called inside a GUI
pass, else the focused window's position plus the event mouse position, else the main window.
Keys are handled at the top of `OnGUI` before the search field sees them. Activation closes the popup
first and navigates in a `delayCall`, because `Navigator.Open` changes focus and selection.
