# NS-45: Safer Revert: detect changed files

Status: Draft
Depends on: NS-9
Area: app

## Why
Revert never overwrites something that was changed after the batch.

## Acceptance criteria
1. Given a file from the batch was modified or removed since, then Revert lists it, skips only that file and reverts the rest after asking.
2. Given a file with the old name now exists (a new file), then it is skipped and never overwritten.
3. Given the user chooses cancel, then nothing is reverted.
4. Given the batch was all clean, then Revert needs no dialog.

## Out of scope
- Anything not listed above.

## Decisions
- Better than classic renamers: classic tools undo blindly. **Decision:** check first, report, never overwrite (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
