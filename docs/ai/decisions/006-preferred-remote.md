# Decision 006 — Fetch/Pull/Push default to the remote currently worked with

- **Date**: 2026-10-15 (`0a582d5e`)
- **Code**: `Repository.GetPreferredRemote()`, used by `ViewModels/Fetch.cs`, `ViewModels/Pull.cs`, `ViewModels/Push.cs`

## Decision

`Repository.GetPreferredRemote()` picks, in order: the remote of the current branch's upstream (falling back to
matching `refs/remotes/<remote>/` when the upstream ref is gone) → `RepositorySettings.DefaultRemote` → `origin` →
the first remote.

- `Fetch` / `Pull` popups: default remote.
- `Push`: only a fallback, when the pushed branch has no `pushRemote` / upstream.

## Why

_Inferred — the commit (`fix upstream remote of the current branch`) has no body; confirm or correct._
In repositories with several remotes (fork + upstream), the popups defaulted to a remote unrelated to the branch
being worked on, which made it easy to fetch/pull/push the wrong one.
