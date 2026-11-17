# Decision 003 — History `Advanced Options` are global

- **Date**: 2026-10-15 (`6430efec`) · revised 2026-11-10 (`1083d68f`)
- **Code**: `ViewModels/Preferences.cs`, `ViewModels/Histories.cs` (`ForEachOpened`), `Models/RepositoryUIStates.cs`
  (`BuildHistoryParams`)

## Decision

- Show flags, commit order, graph highlighting and column visibility of the commit graph live in `Preferences`, the
  same for every repository. A change is applied to every opened repository tab at once (`Histories.ForEachOpened`).
- Per repository stay: `AuthorColumnWidth`, branch/tag visibility filters, and upstream's `Focus on Current Branch`
  (`RepositoryUIStates.FocusOnCurrentBranch`). `BuildHistoryParams` takes the global flags/order as parameters.
- 2026-11-10: the fork's own `Current branch only` show flag was **removed** in favor of upstream's
  `Focus on Current Branch` — both ran `git log HEAD <upstream>`, and upstream's toggle also clears/disables
  branch/tag filters and `Show lost commits` consistently.

## Why

_Inferred from the commits; confirm or correct._

- These are viewing preferences of the user, not properties of a repository: setting them again in every repository
  was tedious.
- Focus on the current branch depends on the repository (it interacts with its filters), so it stays per repository.

## Consequences

- Values saved per repository before 2026-10-15 are not migrated ([`../tech-debt.md`](../tech-debt.md)).
