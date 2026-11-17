# Decision 008 — Where the fork's docs live

- **Date**: 2026-11-17

## Decision

- Docs stay **inside this repository** under `docs/` (public, no private submodule):
  - `docs/ai/todo.md` · `known-issues.md` · `tech-debt.md` · `lessons.md` · `decisions/NNN-*.md`
  - `docs/plans/` — plans of features not started yet.
- `CLAUDE.md` at the root stays the entry point: build commands, architecture, and a short list of fork features
  (the "what"); the "why" goes to `docs/ai/decisions/`.
- `KNOWN_ISSUES.md` at the root was split into `known-issues.md` / `todo.md` / `tech-debt.md`.

## Why

- One module only, and nothing secret: a private submodule (as in other projects) would add a second repo to push
  for no gain.
- Separating "what is wrong now", "what to do", "what to clean up", "what we learned" and "why we chose this" keeps
  each file short and easy to update.
