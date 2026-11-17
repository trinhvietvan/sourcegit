# Known issues

Known limitations and bugs of the fork-specific features (`feature/vantv_dev`), as they behave **today**.
Work still to do is in [`todo.md`](todo.md), code that should be cleaned up in [`tech-debt.md`](tech-debt.md).
When an item is fixed, move it to "Closed" with the date and commit instead of deleting it.

## Hosting accounts (`Preferences > Accounts`)

Code: `Models/HostingAccount.cs`, `Models/HostingAPI.cs`, `Commands/Credential.cs`, `ViewModels/AddHostingAccount.cs`,
`ViewModels/AccountSelector.cs`, `Views/Preferences.axaml(.cs)`, `Views/AddHostingAccount.axaml`.
Why it works this way: [`decisions/001-hosting-accounts.md`](decisions/001-hosting-accounts.md).

- **Sign in with browser only works for GitHub** (`AddHostingAccount.CanSignInWithBrowser`). The account is created by
  calling the provider's `/user` API with the token GCM returns:
  - GitLab: GCM's OAuth token most likely only has `read_repository` + `write_repository` (not verified in GCM's
    source), so `/api/v4/user` is rejected. Self-hosted GitLab also needs a GCM OAuth app to be configured.
  - Bitbucket: OAuth tokens need `Authorization: Bearer` (not `email:token` like API tokens) and git uses the
    username `x-token-auth`.
- With GCM, `Use Browser` runs `git credential-manager github login --browser` (`Commands.GitHubCredentialManager`);
  other credential helpers still use `fill`, so a second account of the same host needs a token there.
- Host field is locked to `github.com` / `gitlab.com` / `bitbucket.org` (`allowCustomHost: false` in
  `HostingProvider.Supported`), although the code supports GitHub Enterprise (`/api/v3`) and self-hosted GitLab.
- `AvatarURL` is stored but not displayed.
- On Linux/macOS the token is saved by whatever `credential.helper` is configured; `AddHostingAccount` reads it back
  and reports an error if nothing stored it.

## SSH keys of accounts

- Keys are passed as `GIT_SSH_COMMAND="ssh -i <key>"` without `-o IdentitiesOnly=yes`, so a key loaded in
  ssh-agent for the same host may be offered first and authenticate as another account. Adding the option changes
  behaviour for every remote.
- Automatic key (`Preferences.FindAccountSSHKey`) is only used when exactly one account of the host has a key.
- Deleting the account's key in `SSH Key Helper` leaves the old path on the account (not used anymore since the
  file is missing).
- `SSH Key Helper` runs `ssh-keygen` from `PATH`, not from the embedded git (`<git>\usr\bin\ssh-keygen.exe`), so
  generating fails if Windows OpenSSH Client is not installed.

## Binding an account to a remote (`Clone` / `Add Remote` / `Edit Remote`)

- `credential.https://<host>.username` is per repository: two HTTPS remotes of the same host can not use different
  accounts (the last one wins).
- Unbinding an account does not revert `user.name` / `user.email` that were applied from it.
- Unbinding an account of an SSH remote keeps its `remote.<name>.sshkey`.

## Commit/Pull/Push all submodules

Code: `ViewModels/CommitSubmodules.cs`, `ViewModels/PullSubmodules.cs`, `ViewModels/PushSubmodules.cs`,
`ViewModels/SubmoduleTrackingInfo.cs`, `ViewModels/SubmoduleBranchTarget.cs`.
Why: [`decisions/005-submodule-branch-target.md`](decisions/005-submodule-branch-target.md).

- Only direct submodules (`git submodule status` without `--recursive`); nested submodules are neither committed,
  pulled nor pushed.
- Commit all: one message for every submodule, no sign-off/no-verify/amend options, and the new submodule commits
  are not staged in the parent repository.
- Pulling does not commit the new submodule commits in the parent repository (it only shows them as changed), and
  pushing submodules does not push the parent.
- Runs sequentially, no per-submodule result summary: failures only show up as notifications and in the command
  log, and the popup always reports success.
- Pull skips any submodule with local changes; there is no "stash and reapply" option like the normal Pull.
- Push skips when `Ahead` is 0, computed from the last fetched upstream ref (a stale remote-tracking ref may
  hide/skip work); `Force push` does not bypass this check.

## Add git to user PATH (Windows)

Code: `Native/WindowsUserPath.cs`, `Views/Preferences.axaml(.cs)` (`UpdateUserPathState`, `SyncUserPathAfterGitChangedAsync`).

- Only the user `Path` (`HKCU`) is supported. New processes get the system `Path` first, so a git in the system
  `Path` (e.g. SourceTree's `...\SourceTree\git_local\cmd`) still wins; the UI only warns about it.
- Only offered to replace the old entry when git is changed by the buttons/file picker, not when the path is typed
  into the `Git Path` text box.
- SourceGit itself keeps the `PATH` it was started with (no effect on the running app).

## Closed

(none yet)
