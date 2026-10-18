# NS-9: Undo journal and Revert

Status: In Progress
Depends on: NS-8
Area: app

## Why
Every batch is logged so it can be undone, even after the app is closed.

## Acceptance criteria
1. Given a successful batch of 3 renames, then a journal entry with time, folder(s) and the old/new full paths of all 3 is stored under `%LOCALAPPDATA%\NameShift\history`.
2. Given an entry exists, when Revert runs, then every file gets its old name back, and afterwards Revert is disabled until a new batch is made (one level of undo; the reverted entry is not undone again and does not expose the batch before it).
3. Given the app was restarted after the batch, then the last batch can still be reverted.
4. Given a failed (rolled back) batch, then no journal entry is written.
5. Given the journal file is corrupt or unreadable, then the app starts normally, Revert is disabled, and the corrupt file is kept untouched.
6. Given a new batch succeeds, then the journal keeps only that batch (it replaces the previous entry), because only the last batch can be reverted.
7. Given the batch contains a chain (`a`->`b`, `b`->`c`), a swap (`a`<->`b`) or a case-only change (`photo.jpg` -> `Photo.jpg`), then Revert restores the old names using the same ordering and temporary-name mechanism as the executor (NS-8), verified in a temp folder for each of the three.
8. Given Revert fails on one file (it was removed or a file now has the old name), then nothing is overwritten; the result lists the files that could not be reverted and the entry stays available for those.

## Out of scope
- Revert safety warnings (NS-45).
- Reverting older batches than the last (not planned: one level of undo is the product).

## Decisions
- Revert only the last batch? **Decision:** yes for now, because one level matches the plan and is the simplest safe behaviour (po, 2026-10-18).
- Keep 50 entries? **Decision:** no, keep only the last batch, because older entries could never be used and would only pile up personal paths on disk (po, 2026-10-18).
- Is the journal a privacy issue? **Decision:** it stays on the PC and is never sent anywhere; it holds only paths the user renamed (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress, branch feature/NS-9-journal (builder).
