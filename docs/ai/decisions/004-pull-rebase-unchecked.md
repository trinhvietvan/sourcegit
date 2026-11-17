# Decision 004 — `Use rebase instead of merge` always starts unchecked

- **Date**: 2026-10-15 (`02fb5833`)
- **Code**: `ViewModels/Pull.cs`, `ViewModels/PullSubmodules.cs`

## Decision

`Pull` and `Pull All Submodules` always open with `Use rebase instead of merge` **unchecked** (merge). The choice is
not remembered: `RepositoryUIStates.PreferRebaseInsteadOfMerge` was removed and the old value in existing UI state
files is ignored.

## Why

_Inferred — the commit does not state the reason; confirm or correct._
Upstream remembered the option per repository and checked it by default, so a rebase could happen without the user
noticing. Rebasing rewrites local commits; it should be a deliberate choice each time.
