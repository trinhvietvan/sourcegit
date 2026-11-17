# Decision 002 — Embedded Git (PortableGit) on Windows

- **Date**: 2026-10-15 (`ad77a84f`) · related: `Add git to user PATH` (`daec441c`)
- **Code**: `ViewModels/EmbeddedGit.cs`, `Native/WindowsUserPath.cs`, `Views/Preferences.axaml(.cs)` (`Preferences > Git`)

## Decision

- `Use Embedded Git` downloads the latest PortableGit from the official git-for-windows GitHub release, **verified by
  the published SHA-256 digest**, into `%LOCALAPPDATA%\SourceGit\git` (or `data\git` in portable mode), updates it
  when a newer release exists, and switches to it. If the update check fails, the installed copy is used.
- `System Git` switches back to the git found in registry/`PATH` **without deleting** the embedded copy, and skips the
  embedded copy when it is found in `PATH`.
- The `Git Bash` shell/terminal path follows the selected git.
- `Add git to user PATH` only touches the user `Path` (`HKCU\Environment\Path`), keeps `REG_EXPAND_SZ` and broadcasts
  `WM_SETTINGCHANGE`.

## Why

_Inferred from the commit; confirm or correct._

- SourceGit needs git >= 2.25.1; a machine without git (or with an old one) should work with one click.
- Only official binaries with a checked digest — no third-party mirror.
- User `Path` needs no elevation; the system `Path` would need an elevated helper (see
  [`../known-issues.md`](../known-issues.md)).
