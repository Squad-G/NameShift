# NS-34: Copy or move to another folder (engine and executor)

Status: Draft
Depends on: NS-8, NS-9
Area: app

## Why
Rename and send the files to another folder in one go.

## Acceptance criteria
1. Given mode copy to `D:\out`, then new files appear there with the new names and the originals stay.
2. Given mode move, then the files are moved and renamed; on the same drive it is a move, across drives a copy then delete of the source only after the copy is verified by size.
3. Given a target that exists, then it is flagged `TargetExists` and nothing is written.
4. Given a failure part way, then moved files are moved back and copied files are removed (only those this batch made).
5. Given a journal entry of a copy, then Revert removes only the copies; for a move it moves them back.
6. Given a target folder that does not exist, then it is created, and removed on rollback only if empty and created by this batch.
7. Given the target is inside the source selection, then it is refused.

## Out of scope
- Anything not listed above.

## Decisions
- Delete copies on Revert? **Decision:** yes only the ones this batch created and only if their size and date are unchanged (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
