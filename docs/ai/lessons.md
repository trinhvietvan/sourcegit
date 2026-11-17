# Lessons

Things learned the hard way while building the fork. If a lesson already has a home (code comment, `CLAUDE.md`, a
decision), just point to it.

## Git / credentials

- **`git credential fill` does not open a sign-in when GCM already has a token for the host** — it just returns the
  stored one, so "add a second GitHub account" silently saved the first account again. Use GCM's own
  `git credential-manager github login --browser` and find the new account by comparing stored tokens before/after
  (`7c3d861e`, `Commands.GitHubCredentialManager`).
- **Every code path that queries remotes by itself must resolve the account SSH key too.** `SubmoduleTrackingInfo`
  queried submodule remotes on its own and never got the key that `Repository.RefreshBranches` applies, so
  pull/push all submodules used the wrong identity (`59b9ca4e`).
- **A detached HEAD is the normal state of a submodule** right after `git submodule update`. Skipping detached
  submodules meant "commit/pull/push all" skipped almost everything (`68ccfb68`,
  [`decisions/005-submodule-branch-target.md`](decisions/005-submodule-branch-target.md)).

## Avalonia

- **Window position restore: don't require the saved bounds to fit in one screen.** A window snapped against the
  taskbar or a screen edge never fits, so it came back centered on the primary screen. Pick the screen showing the
  largest part, move/shrink into its working area, and convert the saved size with **that** screen's scaling
  (`ff2e4d70`, `Views/Launcher.axaml.cs`).
- **A child view can bind to an ancestor view's view model with compiled bindings**:
  `{Binding $parent[v:Repository].((vm:Repository)DataContext).Prop, Mode=TwoWay}` compiles with `x:DataType` set to
  the child's own VM. Useful but couples the views ([`tech-debt.md`](tech-debt.md)).

## Working on the fork

- **Check upstream before adding a fork feature.** The fork's `Current branch only` show flag duplicated upstream's
  later `Focus on Current Branch` (`git log HEAD <upstream>` in both) and was removed in `1083d68f`. After each upstream
  merge, compare the fork features in `CLAUDE.md` with upstream's changelog.
