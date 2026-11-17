# Decision 001 — Hosting accounts never store tokens

- **Date**: 2026-10-15 (`34ace3b7`) · updated 2026-10-22 (`7c3d861e`, browser sign-in for a second account)
- **Code**: `Models/HostingAccount.cs`, `Models/HostingAPI.cs`, `Commands/Credential.cs`,
  `Commands/GitHubCredentialManager.cs`, `ViewModels/AddHostingAccount.cs`, `ViewModels/AccountSelector.cs`

## Decision

- `Preferences > Accounts` stores only account **metadata** (+ optional SSH key path) in `Preferences.HostingAccounts`.
- The token is verified with the provider's `/user` API, then handed to the git credential helper
  (`git credential approve`); removing an account runs `git credential reject`. SourceGit never writes a token to
  `preference.json`.
- Binding an account to a remote uses plain git config: `credential.https://<host>.username` for HTTPS,
  `remote.<name>.sshkey` for SSH, optionally the account's name/email as `user.name` / `user.email`.
- SSH remotes without their own key use the key of the **only** account of their host that has one.

## Why

_Inferred from the commit; confirm or correct._

- The credential helper (GCM on Windows, keychain/libsecret elsewhere) already stores secrets safely and is what git
  itself uses for push/pull — no second secret store to keep in sync or leak.
- Plain git config keeps the binding visible and working from the command line too.

## Consequences

- Behaviour depends on the configured `credential.helper` (see [`../known-issues.md`](../known-issues.md)).
- `git credential fill` returns the stored token when one account exists, so a second GitHub account needs GCM's
  `github login --browser` ([`../lessons.md`](../lessons.md)).
