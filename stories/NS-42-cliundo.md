# NS-42: Command line: undo the last batch

Status: Draft
Depends on: NS-38, NS-45
Area: app

## Why
Undo from the shell.

## Acceptance criteria
1. Given `NameShiftCmd undo`, then the last batch (from the CLI or the window) is reverted using the same journal as Revert.
2. Given no batch, then it prints `nothing to undo` and exits 0.
3. Given files changed since, then it skips and lists them as the window does (NS-45) and exits 4 if any could not be reverted.
4. Given `--dry-run`, then it prints what it would restore and changes nothing.

## Out of scope
- Anything not listed above.

## Decisions
- None needed.

## Log
- 2026-10-18: created (po).
