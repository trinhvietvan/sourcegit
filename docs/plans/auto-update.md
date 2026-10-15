# Plan: auto update from the fork's GitHub releases

Status: **not started** (planned on 2026-10-15). Windows first; macOS/Linux later.

## Current behaviour (upstream)

- `App.Check4Update` (`App.axaml.cs`) downloads `https://sourcegit-scm.github.io/data/version.json` (5s timeout) into
  `Models.Version` (`tag_name`, `name`, `published_at`, `body`) and compares `tag_name` with the assembly version
  (from the `VERSION` file, `SourceGit.csproj`).
- On startup: only if `Preferences > General > Check for updates on startup` and at most once a day
  (`Preferences.ShouldCheck4UpdateOnStartup`, `LastCheckUpdateTime`); a new version only shows an indicator in the
  title bar (`Launcher.NewVersion`); `IgnoreUpdateTag` skips a version. Manually (`Check for Updates...`): shows
  `Views/SelfUpdate` (new version / up to date / failed).
- `Download` only opens `https://github.com/sourcegit-scm/sourcegit/releases/latest` — nothing is installed.
- `-p:DisableUpdateDetection=true` (`DISABLE_UPDATE_DETECTION`) removes all of it.
- **Problem for the fork**: it carries the upstream version (`2026.22`), so every upstream release is offered and
  `Download` leads to the official build without the fork's features.

## What can be reused

- `release.yml`: pushing a tag builds all 6 runtimes and creates a GitHub release with `sourcegit_<ver>.<rid>.zip`
  (Windows zip = the `SourceGit\` folder, portable).
- `ViewModels/EmbeddedGit.cs`: download with progress/cancel, SHA-256 check against the GitHub asset `digest`,
  staging + backup folders.
- GitHub Releases API JSON already matches `Models.Version` (`tag_name`, `name`, `published_at`, `body`).
- Multi-mode executable (`App.axaml.cs`): add a `--apply-update` mode instead of a separate updater.
- Portable mode: config lives in `<exe dir>\data\` when it exists (`Native/Windows.cs`), otherwise `%APPDATA%`.

## Steps

### 0. Release process of the fork
- Own version numbers in `VERSION`, e.g. `2026.22.1` (upstream base + fork patch). `System.Version` parses it; fix
  `Models.Version.CurrentVersionStr` (only prints `Major.Minor` today).
- Enable GitHub Actions on `trinhvietvan/sourcegit`; release = `git tag v2026.22.1 && git push origin v2026.22.1`.

### 1. Check the fork instead of upstream
- Replace the `version.json` URL with `https://api.github.com/repos/trinhvietvan/sourcegit/releases/latest`
  (constant; allow an env override such as `SOURCEGIT_UPDATE_URL` for testing).
- Add `assets` (`name`, `browser_download_url`, `size`, `digest`) to `Models.Version` and register the new types in
  `App.JsonCodeGen.cs` (AOT).
- `Download` opens the fork's releases page.

### 2. Download and verify
- Pick the asset for the running runtime (`sourcegit_<ver>.win-x64.zip` / `win-arm64` via `RuntimeInformation`).
- Download to `%LOCALAPPDATA%\SourceGit\update\` with progress + cancel; verify SHA-256 with `digest`; extract to
  `update\staging\`.

### 3. Apply (Windows)
The running `SourceGit.exe` can not be overwritten, so:
1. Copy the **new** `SourceGit.exe` to a temp folder and start it with
   `--apply-update <staging> <install dir> <pid>`; save preferences/workspace state, then quit.
2. The helper waits for the old process to exit, backs up the install dir to `<install dir>.old`, mirrors staging into
   it **keeping `data\`**, restores the backup on any failure.
3. Start the new `SourceGit.exe`; the next start removes `.old` and `update\`.
- If the install dir is not writable (e.g. `Program Files`), fall back to opening the releases page.

### 4. UX
- `SelfUpdate` dialog: `Update & Restart` with a progress bar; keep `Download` (manual) and `Ignore this version`.
- `Preferences > General`: "Automatically download updates" (download in background, ask only before restart).
- Refuse to restart while a popup/git operation is running.

### 5. Testing
- Pre-release on the fork or a fake JSON through the env override.
- Update an older build → check preferences and `data\` are kept; failure cases: network drop, wrong checksum,
  read-only install dir.

## Later
- macOS: replace the `.app` bundle (code signing, quarantine/Gatekeeper).
- Linux: AppImage = replace one file; `.deb`/`.rpm` → only open the releases page (leave it to the package manager).

## Risks
- Antivirus may flag an app that downloads and runs a new exe.
- GitHub API rate limit without auth: 60 requests/hour/IP (fine for once a day).
