# NS-35: Copy/move to location panel

Status: Draft
Depends on: NS-34, NS-15
Area: app

## Why
Choose copy or move and a destination in the window.

## Acceptance criteria
1. Given a panel with modes (rename in place, copy, move) and a folder picker, then the choice drives the executor.
2. Given the destination is missing or invalid, then Rename is disabled with a reason.
3. Given the mode is copy or move, then the preview shows the destination path.
4. Given reset, then the mode returns to rename in place.

## Out of scope
- Anything not listed above.

## Decisions
- None needed.

## Log
- 2026-10-18: created (po).
