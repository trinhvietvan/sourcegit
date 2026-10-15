# Known Issues / TODO

Open items of the fork-specific features (`feature/vantv_dev`), to be finished later.

## Hosting accounts (`Preferences > Accounts`)

Code: `Models/HostingAccount.cs`, `Models/HostingAPI.cs`, `Commands/Credential.cs`, `ViewModels/AddHostingAccount.cs`,
`ViewModels/AccountSelector.cs`, `Views/Preferences.axaml(.cs)`, `Views/AddHostingAccount.axaml`.

### Sign in with browser (Git Credential Manager)

- [ ] **Only GitHub is supported** (`AddHostingAccount.CanSignInWithBrowser`). The account is created by calling the
  provider's `/user` API with the token GCM returns:
  - GitLab: GCM's OAuth token most likely only has `read_repository` + `write_repository` (not verified in GCM's
    source), so `/api/v4/user` is rejected. Possible "limited mode": take the username from `git credential fill`,
    skip `/user` → no name/email/avatar (hides "use account identity"), no SSH key upload (needs `api`).
    Self-hosted GitLab also needs a GCM OAuth app to be configured.
  - Bitbucket: OAuth tokens need `Authorization: Bearer` (not `email:token` like API tokens) and git uses the
    username `x-token-auth`. Not tested.
- [ ] If GCM already stores a `github.com` credential, `fill` returns it without showing the sign-in UI, so a second
  GitHub account can only be added with a personal access token.

### Bitbucket (not tested with a real account)

- [ ] Token is saved under the Bitbucket **username**; Atlassian documents the static username
  `x-bitbucket-api-token-auth` for API tokens. Check that push/pull works with the real username.
- [ ] GCM's Bitbucket provider may validate stored credentials and pop up "Connect to Bitbucket"
  (see `credential.bitbucketValidateStoredCredentials`).
- [ ] SSH key upload (`POST /2.0/users/{uuid}/ssh-keys`) and the exact API token scope name are not verified
  (UI hint just says "SSH keys (write)").

### Other

- [ ] Host field is locked to `github.com` / `gitlab.com` / `bitbucket.org` (`allowCustomHost: false` in
  `HostingProvider.Supported`). The code already supports GitHub Enterprise (`/api/v3`) and self-hosted GitLab:
  flip the flag to re-enable.
- [ ] `AvatarURL` is stored but not displayed.
- [ ] New `Text.AddHostingAccount.*`, `Text.Preferences.Accounts.*`, `Text.AccountSelector.*` strings exist only in
  `en_US.axaml`.
- [ ] Only tested on Windows with GCM (embedded PortableGit). On Linux/macOS the token is saved by whatever
  `credential.helper` is configured; `AddHostingAccount` reads it back and reports an error if nothing stored it.
- [ ] Idea: "Import from SourceTree" (`%APPDATA%\Atlassian\SourceTree\accounts.json` has the accounts; tokens are
  already in Windows Credential Manager under `git:https://[user@]host`).

## SSH keys of accounts

- [ ] Keys are passed as `GIT_SSH_COMMAND="ssh -i <key>"` without `-o IdentitiesOnly=yes`, so a key loaded in
  ssh-agent for the same host may be offered first and authenticate as another account. Adding the option changes
  behaviour for every remote.
- [ ] Automatic key (`Preferences.FindAccountSSHKey`) is only used when exactly one account of the host has a key.
- [ ] Deleting the account's key in `SSH Key Helper` leaves the old path on the account (not used anymore since the
  file is missing).
- [ ] `SSH Key Helper` runs `ssh-keygen` from `PATH`, not from the embedded git (`<git>\usr\bin\ssh-keygen.exe`), so
  generating fails if Windows OpenSSH Client is not installed.
- [ ] `Repository.RefreshBranches` reads `Preferences.HostingAccounts` on a background thread (possible
  "collection modified" race if accounts change at the same time).

## Binding an account to a remote (`Clone` / `Add Remote` / `Edit Remote`)

- [ ] `credential.https://<host>.username` is per repository: two HTTPS remotes of the same host can not use different
  accounts (the last one wins).
- [ ] Unbinding an account does not revert `user.name` / `user.email` that were applied from it.
- [ ] Unbinding an account of an SSH remote keeps its `remote.<name>.sshkey`.

## Pull/Push all submodules

Code: `ViewModels/PullSubmodules.cs`, `ViewModels/PushSubmodules.cs`, `ViewModels/SubmoduleTrackingInfo.cs`.

- [ ] Only direct submodules (`git submodule status` without `--recursive`); nested submodules are neither pulled nor
  pushed.
- [ ] Pulling does not commit the new submodule commits in the parent repository (it only shows them as changed), and
  pushing submodules does not push the parent. Consider an option "commit submodule pointers" / "push parent too".
- [ ] Runs sequentially, no per-submodule result summary: failures only show up as notifications and in the command
  log, and the popup always reports success.
- [ ] Pull skips any submodule with local changes; there is no "stash and reapply" option like the normal Pull.
- [ ] Push skips when `Ahead` is 0, computed from the last fetched upstream ref (a stale remote-tracking ref may
  hide/skip work); `Force push` does not bypass this check.
- [ ] Pull mode (merge/rebase) is shared with the parent repo's `PreferRebaseInsteadOfMerge` UI state.

## Verification

- [ ] No UI test has been done for any of the above (there is no test project): only `dotnet build` and
  `dotnet format --verify-no-changes`. `git credential approve/fill/reject` was checked by hand with the embedded git.
