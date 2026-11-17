# Tech debt

Code of the fork that works but should be cleaned up. Behaviour problems are in [`known-issues.md`](known-issues.md).
Remove the line once the debt is paid.

- **`Repository.RefreshBranches` reads `Preferences.HostingAccounts` on a background thread** — possible
  "collection modified" race when accounts are added/removed at the same time (`Preferences.FindAccountSSHKey`, also
  called from `SubmoduleTrackingInfo`).
- **`Focus on Current Branch` toggle in `Views/Histories.axaml` binds to the parent view's view model**
  (`$parent[v:Repository].((vm:Repository)DataContext).FocusOnCurrentBranch`, 2026-11-17): `ViewModels.Histories`
  has no property for it, and a pass-through property would miss the change notifications that `Repository` raises
  when it turns the flag off itself (branch/tag filter set manually). The `Histories` view therefore only works inside
  a `Views.Repository`. Same for `Histories.OnNavigateToHead` (`FindAncestorOfType<Repository>`).
- **History `Advanced Options` moved to `Preferences` without migration** (2026-10-15, `6430efec`): values saved per
  repository before are ignored. Same for the removed `RepositoryUIStates.PreferRebaseInsteadOfMerge` (`02fb5833`).
- **Fork strings only in `en_US.axaml`** — not translated, and `TRANSLATION.md` has not been regenerated since they
  were added (`node build/scripts/localization-check.js`).
- **No test project** (upstream has none either): every change is only verified by `dotnet build`,
  `dotnet format --verify-no-changes` and running the app by hand.
