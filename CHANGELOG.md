# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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
