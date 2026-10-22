# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

SourceGit is a cross-platform (Windows/macOS/Linux) Git GUI client written in C# on .NET 10 + Avalonia 11 (MVVM via CommunityToolkit.Mvvm). It shells out to the user's installed `git` (>= 2.25.1) for all repository operations — there is no libgit2.

## Commands

```sh
git submodule update --init          # required: depends/AvaloniaEdit is a custom fork, referenced as a ProjectReference
dotnet build                         # builds SourceGit.slnx (app + AvaloniaEdit)
dotnet run --project src/SourceGit.csproj
dotnet format --verify-no-changes src/SourceGit.csproj   # CI format gate; drop --verify-no-changes to fix
dotnet publish src/SourceGit.csproj -c Release -r <rid> -o <dir>   # rid: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64
```

- Release builds use **NativeAOT + full trimming** (`PublishAot`, `TrimMode=link`). Pass `-p:DisableAOT=true` to skip. `-p:DisableUpdateDetection=true` defines `DISABLE_UPDATE_DETECTION`.
- There is no test project. Verify changes by building and running the app.
- Localization check (CI runs it on `develop`; it also re-sorts locale files and regenerates `TRANSLATION.md`): `node build/scripts/localization-check.js` (needs `fs-extra`, `path`, `xml2js`).
- Interactive translation helper: `python translate_helper.py <lang_id> [--check]`.
- PRs target the `develop` branch, not `master`.

## Architecture

All app code lives in `src/` under the `SourceGit` namespace, split by folder:

- **`Commands/`** — one class per git operation, each subclassing `Commands.Command` (`Command.cs`). A command sets `WorkingDirectory`, `Context` (repo path, used to route error notifications), `Args` (everything after `git`), and optionally `SSHKey`/`Editor`/`RaiseError`. Mutating commands call `ExecAsync()`; query commands (`Query*`) call `ReadToEndAsync()` and parse stdout in a `GetResultAsync()`. `CreateGitStartInfo` injects `--no-pager -c core.quotepath=off`, the credential helper, `core.editor`, and forces `LANG=C` on Unix so output is parseable — keep git invocations going through this class. Errors are surfaced via `Models.Notification.Send(Context, ...)` rather than exceptions.
- **`Models/`** — plain data types (Branch, Commit, Change, Remote...), parsers, settings (`RepositorySettings`, `RepositoryUIStates`), `Watcher` (filesystem watcher that triggers `IRepository.Refresh*` calls).
- **`ViewModels/`** — `ObservableObject`-based VMs. Key ones:
  - `Launcher` → tabs of `LauncherPage` → each page hosts a `Repository` (or `Welcome`) and an optional `Popup`.
  - `Repository` is the central per-repo state (branches, tags, commits, working copy, settings, watcher). Operations that mutate the repo typically wrap work in `using var _ = _repo.LockWatcher();` so the file watcher doesn't refresh mid-operation, then call the appropriate `Refresh*`.
  - `Preferences` — global settings singleton, serialized to `preference.json`.
- **`Views/`** — Avalonia `.axaml` + code-behind. **ViewModel→View resolution is by naming convention**: `ControlExtensions.CreateFromViewModels` replaces `.ViewModels.` with `.Views.` in the type name (e.g. `ViewModels.Fetch` → `Views.Fetch`). A new popup/command palette/window VM needs a same-named View.
- **`Native/`** — `OS` static facade over per-platform `IBackend` (`Windows.cs`, `MacOS.cs`, `Linux.cs`): git executable discovery, data directories, external editors/merge tools, terminal, file manager.
- **`AI/`** — OpenAI/Azure OpenAI client used to generate commit messages (OpenAI-compatible endpoints).
- **`Resources/`** — `Locales/*.axaml` (string resources), `Themes.axaml`, `Icons.axaml` (StreamGeometry icons), `Styles.axaml`, TextMate grammars, fonts.

### Popup pattern (most user actions)

Most dialogs are a `ViewModels.Popup` subclass (extends `ObservableValidator`, so validation attributes work) plus a matching `Views/<Name>.axaml`. The VM exposes bindable properties and overrides `Task<bool> Sure()` to run the git command(s); return `true` to close. Typical body: `LockWatcher()`, `var log = _repo.CreateLog("..."); Use(log);`, run `new Commands.X(...).Use(log).ExecAsync()`, `log.Complete()`. Set `CanTerminate = true` and override `Terminate()` for cancellable ops. Show with `repo.ShowPopup(...)` / `ShowAndStartPopupAsync(...)` (check `repo.CanCreatePopup()` first). `LauncherPage.ProcessPopupAsync` drives `Check()` → `Sure()`.

A popup can rename its primary button (`Popup.SureButtonText`, default `OK`) and add a second confirm button (`AlternativeSureButtonText`); `Sure()` reads `IsAlternativeSure` to know which one was clicked (fork addition, see `ViewModels/CommitSubmodules.cs`).

### Multi-mode executable (`App.axaml.cs`)

The same binary is re-invoked by git as helper programs, selected by args/env before normal startup: `--rebase-todo-editor` / `--rebase-message-editor` (interactive rebase), `--core-editor`, SSH askpass (`SOURCEGIT_LAUNCH_AS_ASKPASS` env), plus user-facing `--history <path>` and `--blame <file>`. `Command.CreateGitStartInfo` wires these up via `-c core.editor=...`, `sequence.editor`, and `SSH_ASKPASS`.

## Conventions and gotchas

- **AOT/trim safety**: JSON serialization uses the source-generated context in `App.JsonCodeGen.cs`. Any new type persisted to/loaded from JSON must be added there as `[JsonSerializable(typeof(...))]`. Avoid reflection-heavy APIs. Avalonia compiled bindings are on by default (`x:DataType` required in axaml).
- **Localization**: UI strings are `x:String` resources keyed `Text.*` in `src/Resources/Locales/en_US.axaml` (the reference locale). Use `{DynamicResource Text.Foo}` in axaml and `App.Text("Foo", args...)` in C# (the `Text.` prefix is added automatically). Add new keys to `en_US.axaml`; other locales may lag and are tracked in `TRANSLATION.md`.
- Code style is enforced by `.editorconfig` + `dotnet format` in CI: Allman braces, 4-space indent, private fields `_camelCase` declared at the bottom of the class.

## Fork-specific features

Branch `feature/vantv_dev` (compare with `git log master..feature/vantv_dev`):

- **Commit/Pull/Push all submodules**: `More Actions` dropdown in the SUBMODULES sidebar header (`Views/Repository.axaml.cs` `OnOpenSubmoduleGroupMenu`) opens `ViewModels.CommitSubmodules` / `ViewModels.PullSubmodules` / `ViewModels.PushSubmodules`. Commit: `Repository.CommitSubmodulesAsync` lists the initialized submodules that really have local changes (`CountLocalChanges`), then each one is staged (`add --all`, or `--update` without untracked) and committed with the same message. `ViewModels.SubmoduleBranchTarget` decides per submodule for commit, pull and push alike (shown in each popup via `Views.SubmoduleBranchTargetPresenter`, re-checked by `EnsureOnBranchAsync` right before acting): on a branch → use it; detached HEAD exactly at the tip of a branch (`.gitmodules` branch → remote default branch via `Commands.QueryRemoteHead` → the only branch at HEAD) → checkout it first (`checkout -b <name> <remote>/<name>` when no local branch exists); otherwise skipped. Its buttons are `COMMIT` and `COMMIT & PUSH` (the latter pushes each committed branch to its upstream via `SubmoduleTrackingInfo`). Each initialized submodule is pulled/pushed sequentially on its current branch against its upstream (`ViewModels.SubmoduleTrackingInfo` resolves local branch/upstream/remote). Skipped (and logged) when HEAD is detached (and not at the tip of a branch, see above), there is no upstream, local changes exist (pull) or nothing is ahead (push, unless `Push all tags`). `Use rebase instead of merge` of `Pull` / `Pull All Submodules` always starts unchecked and is not remembered (`RepositoryUIStates.PreferRebaseInsteadOfMerge` was removed). Failures of one submodule do not stop the others.
- **Commit graph `Current branch only` show flag**: really hides other branches by querying `HEAD <upstream>` instead of `--branches --remotes --tags HEAD`; branch/tag visibility filters are ignored while it is on.
- **History `Advanced Options` are global**: show flags, commit order, graph highlighting and column visibility live in `Preferences` (not `RepositoryUIStates`); `Histories.ForEachOpened` applies a change to every opened repository tab. `AuthorColumnWidth` and branch/tag visibility filters stay per repository.
- **Fetch/Pull/Push pick the remote currently worked with**: `Repository.GetPreferredRemote()` (upstream remote of the current branch → `RepositorySettings.DefaultRemote` → `origin` → first remote) is the default remote of the `Fetch` / `Pull` popups and the fallback of `Push` (when the pushed branch has no `pushRemote`/upstream).
- **Toolbar `Discard all changes` button** (after `Apply Patch`, `Views/RepositoryToolbar.axaml`): opens the same `Discard` popup as the working copy's `DISCARD ALL` (`Repository.DiscardAllChanges` → `WorkingCopy.DiscardAllChanges`). In the working copy itself, `DISCARD ALL` is moved from the filter row into the UNSTAGED header, before `View Assume Unchanged`.
- **Local changes layout** (`Views/WorkingCopy.axaml`): STAGED is on top and UNSTAGED at the bottom (stage buttons point up, unstage buttons point down); change lists have an 8px bottom padding (`Views/ChangeCollectionView.axaml`).
- **Main window position restore** (`Views/Launcher.axaml.cs` constructor): the saved bounds are restored on the screen showing the largest part of them and moved/shrunk into its working area (upstream only restored them when they fit entirely in one screen, so a window that touched the taskbar/edge came back centered on the primary screen).
- **Sidebar group headers**: TAGS (`Show as tree`, `Sort`) and SUBMODULES actions are moved into a `More Actions` dropdown so every header shows at most two buttons.
- **Embedded Git** (Windows): `ViewModels/EmbeddedGit.cs`, `Preferences > Git`. `System Git` skips the embedded copy when it is found in `PATH`.
- **Add git to user PATH** (Windows): `Preferences > Git > User PATH` adds/removes the selected git's `cmd` folder in `HKCU\Environment\Path` (`Native/WindowsUserPath.cs`, keeps `REG_EXPAND_SZ`, broadcasts `WM_SETTINGCHANGE`), shows which `git.exe` new terminals resolve, and offers to replace the entry when the selected git changes.
- **Hosting accounts**: `Preferences > Accounts`. Tokens are never stored by SourceGit; they go through the git credential helper via `Commands.Credential` (`git credential approve|reject|fill`, non-interactive). Accounts (metadata + optional SSH key) live in `Preferences.HostingAccounts`. `Use Browser` (GitHub) runs GCM's `github login --browser` so another account than the stored one can be added. `ViewModels.AccountSelector` binds an account to a remote (`credential.https://<host>.username` for HTTPS, `remote.<name>.sshkey` for SSH).
- Open items / things to finish later are tracked in `KNOWN_ISSUES.md`.
- Planned (not started) features have a plan in `docs/plans/` (e.g. `docs/plans/auto-update.md`: auto update from the fork's GitHub releases).
