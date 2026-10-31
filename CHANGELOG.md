# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.4.2] - 2026-10-31
### Added
- A colored tool pin now colors the windows it opens (Windows section, window switcher, tab dot and
  strip) unless the window has its own color. Preferences → *Tool pin colors also color their windows*.
- Preferences → *Close tear-off panels with the main window* (off by default).

## [0.4.1] - 2026-10-31
### Added
- Tear-off panels: any section can be opened as its own small floating window (⋮ → *Float section as
  window*, or right-click a section header / tab → *Open in floating window*); restored on startup.
- Profiler markers (`ProjectNavigator.*`) for the periodic work, visible with "Profile Editor" enabled.
### Changed
- Window colors: the per-second pass lists open windows once instead of once per window, and does
  nothing at all while no window is colored.
- Windows section: rows are rebuilt only when the set of open windows, their titles, focus or docking changed.

## [0.4.0] - 2026-10-29
### Added
- Configurable sections: each section can be shown or hidden per window kind (docked / floating) and per
  mode (normal / compact), reordered, and laid out stacked or as tabs (⋮ menu in the window, Preferences).
- Tools section: pin any menu item (searched through Unity Search's menu provider) or editor window type;
  launch with one click, from the popup, or from the main-toolbar dropdown. Team or personal scope.
- Windows section and window switcher popup (Alt+Shift+W): open editor windows with focus/docked state,
  recently used (closed) windows, labels, and colors shown as a dot in the tab and a strip at the top.
- Global per-user file (application-data folder) for tool pins, window marks, recent windows and layout,
  shared by all projects on the machine; project data stays in ProjectSettings/UserSettings.
- Shortcuts: Window Switcher (Alt+Shift+W), Pin Focused Window (Alt+Shift+K), Pin a Tool... (unbound).

## [0.3.2] - 2026-10-27
### Added
- Recovery of data from legacy `ProjectNavigator.asset` files (versions up to 0.3.0) whose script
  reference no longer resolves: automatic on first load when no JSON exists, and a
  "Recover from legacy .asset files" button in Preferences that merges into current data.
- Version footer (with GitHub link) in Preferences and Project Settings; version in the Prefs button tooltip.
### Fixed
- Navigator toolbar: text buttons get padding and no longer get squeezed by the search field.

## [0.3.1] - 2026-10-27
### Changed
- Settings are now stored as JSON (`ProjectSettings/ProjectNavigator.json`, `UserSettings/ProjectNavigator.json`).
  The previous `.asset` files referenced the settings script by GUID and loaded empty after the package
  was reinstalled with new .meta files; JSON has no such dependency. Legacy `.asset` files are still read
  once when their script reference resolves.
- `.meta` files with stable GUIDs are part of the package and should be committed.

## [0.3.0] - 2026-10-27
### Added
- Popup at cursor (Alt+Shift+Space): filterable panel with pinned/frequent/recent entries that closes on focus loss.
- Folder rows: single click selects, double-click opens in the Project window (new default);
  the old single-click-opens behavior is available in Preferences → Folder rows.
- Asset Store compliant navigation: entering folders now works through public editor APIs only.
  The reflection-based path lives in the optional file `Editor/Core/ProjectBrowserBridge.Internals.cs`.
- Menu: Window → Project Navigator (Navigator Window, Floating Navigator, Quick Jump, Preferences, Team Settings).
- Apache-2.0 license, NOTICE, Third-Party Notices, SPDX headers.
### Changed
- Package renamed to `com.eigensoundlab.project-navigator`. Data files are unchanged.

## [0.2.0] - 2026-10-13
### Added
- Files can be pinned, colored and annotated like folders; optional "Recent files" list.
- Notes on folders and files (Navigator rows, Project window list rows, Inspector strip, search).
- Floating window with "stay on top" (utility window), restored on startup.
- "Rename on disk..." (the pin label never renames the asset).

## [0.1.1] - 2026-10-13
### Fixed
- Compile error on Unity 6000.6 (`GetInstanceID` obsolete → `GetEntityId`).

## [0.1.0] - 2026-10-13
- First scaffold: pins, frequent/recent folders, colors with tags/rules/inheritance,
  Project window overlay, Inspector strip, main-toolbar element, `nav:` search provider, shortcuts.
