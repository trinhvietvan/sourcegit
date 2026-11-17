# TODO

Work still to do on the fork (`feature/vantv_dev`). Current limitations are in [`known-issues.md`](known-issues.md);
features that are planned but not started have a plan in [`../plans/`](../plans/).
Tick an item when done (with date/commit), remove it at the next cleanup.

## Verify by hand (there is no test project)

- [ ] No UI test has been done for the fork features: only `dotnet build` and `dotnet format --verify-no-changes`.
  `git credential approve/fill/reject` was checked by hand with the embedded git.
- [ ] `Use Browser` with a real **second** GitHub account (GCM `github login --browser`, account picked by comparing
  stored tokens before/after).
- [ ] Bitbucket with a real account:
  - token is saved under the Bitbucket **username**; Atlassian documents the static username
    `x-bitbucket-api-token-auth` for API tokens — check that push/pull works with the real username;
  - GCM's Bitbucket provider may pop up "Connect to Bitbucket" (`credential.bitbucketValidateStoredCredentials`);
  - SSH key upload (`POST /2.0/users/{uuid}/ssh-keys`) and the exact API token scope name (UI hint says
    "SSH keys (write)").
- [ ] Hosting accounts on Linux/macOS (only tested on Windows with GCM + embedded PortableGit).
- [ ] History header: look at the spacing of `Navigate to HEAD` / `Focus on Current Branch` after `Advanced Options`
  (`Views/Histories.axaml`, `Margin="10,0,0,0"`) in the running app (2026-11-17).

## Features / improvements

- [ ] GitLab "limited mode" for `Use Browser`: take the username from `git credential fill`, skip `/user` → no
  name/email/avatar (hides "use account identity"), no SSH key upload (needs `api`).
- [ ] Re-enable custom hosts (GitHub Enterprise, self-hosted GitLab): flip `allowCustomHost` in
  `HostingProvider.Supported` and test.
- [ ] Display `AvatarURL` in the accounts list.
- [ ] Translate `Text.AddHostingAccount.*`, `Text.Preferences.Accounts.*`, `Text.AccountSelector.*` (only in
  `en_US.axaml`).
- [ ] Submodules: option "commit submodule pointers" / "push parent too"; "stash and reapply" for pull; per-submodule
  result summary in the popup.
- [ ] System `Path` for `Add git to user PATH`: needs an elevated helper (e.g. re-run
  `SourceGit.exe --set-machine-path` with `runas`).
- [ ] Idea: "Import from SourceTree" (`%APPDATA%\Atlassian\SourceTree\accounts.json` has the accounts; tokens are
  already in Windows Credential Manager under `git:https://[user@]host`).
- [ ] Auto update from the fork's GitHub releases: [`../plans/auto-update.md`](../plans/auto-update.md).
