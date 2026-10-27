# Contributing

Issues and pull requests are welcome.

- Keep the editor assembly free of reflection into Unity internals except inside
  `Editor/Core/ProjectBrowserBridge.Internals.cs`. That file is removed for Asset Store builds
  (store guideline 2.5.g), so everything else must work without it.
- Store assets by GUID, never by path or object reference.
- Static state must be initialized idempotently (domain reload on Play is off by default in Unity 6.6).
- Releasing: bump `version` in `package.json` and `NavigatorVersion.Fallback`, add a CHANGELOG entry, tag `vX.Y.Z`,
  then attach the zip from `Tools~/make-release-zip.sh` to the GitHub release.
- Run the EditMode tests (Window → General → Test Runner) before opening a pull request.

By submitting a contribution you agree that it is licensed under the Apache License 2.0, like the
rest of the project (see LICENSE, section 5 "Submission of Contributions"). Asset Store releases
are prepared from the author's own code; if that ever changes, contributors will be asked for
explicit permission first.
