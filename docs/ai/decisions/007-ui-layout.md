# Decision 007 — UI layout changes of the fork

Small layout decisions, newest first. Each keeps the upstream feature and only moves/rearranges it.

## 2026-11-17 — `Navigate to HEAD` / `Focus on Current Branch` in the commit list header

- Moved from the repository toolbar (next to the current branch name) into the commit list header of
  `Views/Histories.axaml`: `Search Commits` → `Advanced Options` (upstream position, unchanged) → small gap →
  `Navigate to HEAD` → `Focus on Current Branch`.
- First tried between `Search Commits` and `Advanced Options`; rejected (bad UX — it moved a button users already
  knew). Keep upstream buttons where they are and append fork-moved ones after a gap.
- Binding of the toggle: [`../tech-debt.md`](../tech-debt.md).

## 2026-10-20 — Main window position restore (`ff2e4d70`)

Restore the saved bounds on the screen showing the largest part of them, moved/shrunk into its working area
(upstream only restored them when they fitted entirely in one screen). See [`../lessons.md`](../lessons.md).

## 2026-10-15 — Local changes panel (`a06f47c3`)

STAGED on top, UNSTAGED at the bottom (stage buttons point up, unstage buttons point down); change lists have an 8px
bottom padding. `DISCARD ALL` moved from the filter row into the UNSTAGED header, before `View Assume Unchanged`.

## 2026-10-15 — Toolbar `Discard all changes` (`703b566f`)

Button after `Apply Patch` opening the same `Discard` popup as the working copy's `DISCARD ALL`.

## 2026-10-15 — Sidebar group headers (`17031cf0`)

TAGS (`Show as tree`, `Sort`) and SUBMODULES actions moved into a `More Actions` dropdown, so every header shows at
most two buttons.
