# Decision 005 — Commit/Pull/Push all submodules and the branch to act on

- **Date**: 2026-10-15 (`978f87db` pull/push all, `0e19cbc2` commit all) · 2026-10-16 (`68ccfb68` detached HEAD)
- **Code**: `ViewModels/CommitSubmodules.cs`, `ViewModels/PullSubmodules.cs`, `ViewModels/PushSubmodules.cs`,
  `ViewModels/SubmoduleTrackingInfo.cs`, `ViewModels/SubmoduleBranchTarget.cs`,
  `Views/SubmoduleBranchTargetPresenter.axaml`, `Repository.CommitSubmodulesAsync`

## Decision

- Each initialized submodule is handled **sequentially** on its current branch against its upstream; a failure in one
  does not stop the others.
- `SubmoduleBranchTarget` decides per submodule, for commit, pull and push alike:
  - on a branch → use it;
  - detached HEAD **exactly at the tip** of a branch (`.gitmodules` branch → remote default branch via
    `Commands.QueryRemoteHead` → the only branch at HEAD) → checkout that branch first
    (`checkout -b <name> <remote>/<name>` when there is no local branch);
  - otherwise → skipped, and the reason is logged.
- The popup shows the target of every submodule; it is re-checked by `EnsureOnBranchAsync` right before acting.
- Commit all only commits submodules that really have local changes (`CountLocalChanges`); `COMMIT & PUSH` pushes each
  committed branch to its upstream.
- Skipped: no upstream, local changes (pull), nothing ahead (push, unless `Push all tags`).

## Why

- A detached HEAD is the normal state right after `git submodule update`; skipping it made the feature useless.
- Checking out the branch at the tip does **not move HEAD**, so nothing can be lost. Any other detached state may hold
  commits that are on no branch — the user must decide.

## Consequences

Limits (nested submodules, no parent commit/push, no summary…) are listed in
[`../known-issues.md`](../known-issues.md).
