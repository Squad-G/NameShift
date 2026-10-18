# NS-48: Safe handling of hard cases

Status: Draft
Depends on: NS-8
Area: engine

## Why
Edge cases that classic renamers get wrong.

## Acceptance criteria
1. Given names that differ only by Unicode form (`é` as one character vs `e` + accent), then they are treated as different but both normalised to the same form in the preview text compare; duplicates by canonical equivalence are flagged.
2. Given a read-only file, then it is renamed (rename needs no write to content) and keeps its attribute.
3. Given a file open in another program, then the failure message names the file and says it is in use.
4. Given a shortcut or file with a very long name that is already allowed, then it is untouched.
5. Given the network drive or removable drive disappears mid batch, then the batch is rolled back as in NS-8 or reported if the rollback cannot run.

## Out of scope
- Anything not listed above.

## Decisions
- Better than classic renamers. **Decision:** tested with real temp folders; no admin elevation is ever requested (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
