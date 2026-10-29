# Project Navigator

Fast movement through a Unity project: pinned, frequent and recent folders and files, folder
colors and notes, back/forward navigation, a floating panel and a quick-jump search.
Editor-only, nothing is written into `Assets/` or `.meta` files.

Requires **Unity 6.6 (6000.6) or newer**. License: Apache-2.0.
Author: Daniel Sandner — [eigensound.com](https://eigensound.com/) (sound design and research).
Repository: [github.com/eigensoundlab/project-navigator](https://github.com/eigensoundlab/project-navigator).

<img width="600" height="300" alt="project-navigator-unity-screenshot" src="https://github.com/user-attachments/assets/db1897a6-ea44-4848-8819-f23be1451fa6" />


## Install

Pick one:

1. **Git URL** (needs git on your machine): Window → Package Manager → `+` → *Add package from git URL...* →
   `https://github.com/eigensoundlab/project-navigator.git`
   Add `#v0.3.2` (or another tag) to pin a version; without a tag you get the latest commit on `main`.
   The package lands read-only in `Library/PackageCache`, which is what you want as a user.
2. **Zip into Packages/**: download a release zip from the
   [Releases page](https://github.com/eigensoundlab/project-navigator/releases) and put the extracted
   `com.eigensoundlab.project-navigator` folder into your project's `Packages/` directory. GitHub's
   *Code → Download ZIP* works too; its folder is named `project-navigator-main`, which Unity accepts
   as well (it reads the name from `package.json`), rename it if you like.
3. **OpenUPM** (planned): once listed, `openupm add com.eigensoundlab.project-navigator`.

The tool starts working immediately: it records the folders you browse even while its window is closed.

## Quick start

1. **Window → Project Navigator → Navigator Window.** Dock it anywhere (it is useful as a narrow
   column next to the Project window).
2. **Pin something:** right-click a folder or file in the Project window → *Project Navigator → Pin*,
   or drag it onto the Navigator window.
3. **Color something:** right-click → *Project Navigator → Color → Yellow / Green / ... / Custom...*
   The folder icon takes the color in the Project window; files get a colored bar.
4. **Add a note** to same-named folders so you can tell them apart: right-click → *Project Navigator → Note...*
5. **Move around:** click a row to select it, double-click a folder to open it in the Project window,
   `Alt+Left` / `Alt+Right` to go back and forward, `Alt+Shift+Space` for the popup at the cursor,
   `Alt+Shift+F` to search any folder by name.

## The window

| Section | Contents |
| --- | --- |
| **Pinned** | Your bookmarks. Right-click a row for *Pin label*, *Pin group*, *Move up/down*. Team pins show a `team` chip. |
| **Frequent** | Folders you use most, ranked by recent frequency. Promote one with *Pin*. |
| **Recent** | Folders you browsed, newest first. Survives editor restarts. |
| **Recent files** | Files you selected (off by default: Preferences → *Track selected files*). |
| **Colored** | Every colored folder and file, grouped by color tag. |

Row actions (right-click): Open, Select and Ping, Open Asset, Reveal in Explorer/Finder, Copy Path,
Pin/Unpin, Note, Color, Rename on disk, Forget.

- Drag a row into any Object field or into the Hierarchy.
- Drag assets from the Project window onto a **folder row** to move them there (asks first).
- The search field filters all sections by name, path or note.
- **Compact** shows only the pins; good for a slim docked strip.
- **Float** opens a second, floating copy that stays above the main editor window
  (see *Floating window* below).

## Popup at cursor

`Alt+Shift+Space` (or Window → Project Navigator → Popup at Cursor) opens a small panel at the mouse
position with your pinned, frequent and recent entries. Type to filter by name, path or note,
`↑`/`↓` to move, `Enter` to open (folders open in the Project window, files are selected;
`Shift+Enter` opens a file), `Ctrl+P` to pin or unpin the highlighted entry, `Esc` or clicking
anywhere else closes it. Since the panel only exists while it has focus, it is never hidden behind
other windows: the reliable alternative to an "always on top" panel.

## Other places the tool shows up

- **Main toolbar:** `<` `>` buttons and a *Folders* dropdown with pinned / frequent / recent entries.
  Right-click the toolbar to hide or move it.
- **Project window:** colored folder icons, a small dot on pinned items, notes right-aligned in list
  rows, a colored bar on colored files.
- **Inspector:** with a folder selected, a strip under the header: color, Pin, Open, Note.
  (Optional for files: Preferences → *Also show the strip for files*.)
- **Search (Ctrl+K):** type `nav:` followed by part of a name, path or note. `Alt+Shift+F` opens it
  directly. Enter opens the result.

## Shortcuts

All rebindable in Edit → Shortcuts, group "Project Navigator".

| Default | Action |
| --- | --- |
| Alt+Left / Alt+Right | Back / Forward through browsed folders |
| Alt+Up | Parent folder |
| Alt+Shift+N | Navigator window |
| Alt+Shift+Space | Popup at cursor |
| Alt+Shift+F | Quick jump (search) |
| Alt+Shift+P | Pin / unpin the current folder |
| Alt+1 … Alt+9 | Open pin 1–9 |

Unity's own selection history (Ctrl+Shift+[ / ]) is untouched.

## Colors, rules and notes

- A **tag** is a named color. Assets reference tags, so changing a tag's color recolors every asset
  using it. Manage tags in **Project Settings → Project Navigator**.
- A folder's color is, in order: its own explicit color → the first matching **rule** → the color
  inherited from the nearest colored parent (when the tag has *Inherit* on). *None* blocks
  inheritance; *Clear* removes the explicit color again.
- **Rules** color folders by path pattern (`**/Resources`, `Assets/Art/**`). They are off by
  default; enable them in Project Settings.
- Colors and notes are saved for the **team** by default (`ProjectSettings/ProjectNavigator.asset`,
  commit it). Switch to personal in Preferences → *New colors and notes are team-shared*.

## Floating window and "always on top"

The floating Navigator is a Unity *utility* window: it stays above the main editor window and all
docked views. Unity offers no way to keep a window above *other floating windows* or above other
applications, and no z-order priority exists in its API, so when another floating tool window is
focused it will cover the Navigator. If you work with several floating windows, use the popup at
cursor (`Alt+Shift+Space`), the main-toolbar *Folders* dropdown or the shortcuts instead.

## Where data is stored

| File | Contents | Version control |
| --- | --- | --- |
| `ProjectSettings/ProjectNavigator.json` | tags, rules, team pins, team colors, team notes | commit |
| `UserSettings/ProjectNavigator.json` | history, frequency, personal pins/colors/notes, preferences | ignored by default |

Everything is keyed by asset GUID, so renames and moves keep pins, colors and notes attached.
The files are plain JSON, so they also survive reinstalling, renaming or moving the package itself.
*Pin label* only changes the text shown in the Navigator; *Rename on disk...* renames the real asset.

## Asset Store builds

The store's submission guidelines forbid reflection into editor internals (2.5.g). This package
uses public APIs everywhere except in one optional file, `Editor/Core/ProjectBrowserBridge.Internals.cs`,
which adds exact "which folder is being browsed" tracking. Delete that file (or run
`Tools~/make-store-build.sh`) for a store build; the tool keeps working through the public path.

## Troubleshooting

- *Pins and colors disappeared after replacing the package folder* → versions up to 0.3.0 stored
  data in `.asset` files tied to the package's script GUIDs. Preferences → *Recover from legacy
  .asset files* merges that data back; since 0.3.1 the JSON files are independent of the package.
- *Double-click does not enter a folder in the two-column layout* → Preferences → Project Navigator
  shows which navigation path is active; please report your Unity version.
- *Nothing shows in Recent* → select or browse a folder under `Assets/`; `Packages/` is ignored by
  default (Preferences → *Ignore assets under Packages/*).
- *Colors look wrong after changing a tag* → they refresh on the next Project window repaint;
  hover over the window.

## Development

The repository root *is* the package (`package.json` at the top level). To work on it:

1. Create or open a Unity 6.6 test project.
2. Clone the repository **into** `Packages/com.eigensoundlab.project-navigator` of that project
   (GitHub Desktop → *Clone* → choose that folder, or `git clone https://github.com/eigensoundlab/project-navigator.git Packages/com.eigensoundlab.project-navigator`).
   Unity treats it as an embedded package: editable in the IDE, and the folder is your git working copy.
3. Commit the `.meta` files together with the code; they carry the asset GUIDs.
4. Tag releases (`git tag v0.3.1 && git push --tags`) so users can pin a version:
   `https://github.com/eigensoundlab/project-navigator.git#v0.3.1`.

Installing from the git URL (first install option above) puts a read-only copy into `Library/PackageCache`;
that is for users, not for development. `Tools~/make-store-build.sh` produces the Asset Store flavor.

## License

Apache License 2.0 — see `LICENSE` and `NOTICE`. Contributions are welcome, see `CONTRIBUTING.md`.
