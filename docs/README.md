# Docs of the fork (`feature/vantv_dev`)

Entry point: [`../CLAUDE.md`](../CLAUDE.md) (build, architecture, list of fork features).

| Path | What goes there |
|---|---|
| [`ai/todo.md`](ai/todo.md) | Work still to do, things to verify by hand |
| [`ai/known-issues.md`](ai/known-issues.md) | Current limitations / bugs of fork features (fixed ones move to "Closed") |
| [`ai/tech-debt.md`](ai/tech-debt.md) | Code that works but should be cleaned up |
| [`ai/lessons.md`](ai/lessons.md) | Things learned the hard way |
| [`ai/decisions/`](ai/decisions/) | One file per decision: `NNN-short-name.md` with date, commit, decision, why |
| [`plans/`](plans/) | Plans of features not started yet |

Rules:

- App code paths are relative to `src/` (`ViewModels/Pull.cs`), as in `CLAUDE.md`; other paths are relative to the
  repository root.
- A "Why" not written down at the time is marked _Inferred_ until confirmed.
- Cite the commit (`abc1234`) and the date (`YYYY-MM-DD`) when recording a decision, lesson or fix.
- A new fork feature: add a line to `CLAUDE.md` "Fork-specific features", a decision if a choice was made, and its
  open items to `todo.md` / `known-issues.md`.
