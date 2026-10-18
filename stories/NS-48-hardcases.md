# NS-48: Safe handling of hard cases

Status: Draft
Depends on: NS-8
Area: engine

## Why
Edge cases that classic renamers get wrong.

## Acceptance criteria
1. Given two new names that differ only by Unicode form (`é` as one character vs `e` followed by a combining accent), then both items are flagged `DuplicateTarget`, because they look identical to the user even though NTFS stores them as different files; a new name that equals an existing file only in canonical form is flagged `TargetExists` the same way.
2. Given a read-only file, then it is renamed (rename needs no write to content) and keeps its attribute.
3. Given a file open in another program, then the failure message names the file and says it is in use.
4. Given a file whose name is unchanged by the rules, even if that name is long (over 200 characters but allowed), then it is not touched and not flagged.
5. Given the network drive or removable drive disappears mid batch, then the batch is rolled back as in NS-8 or reported if the rollback cannot run.

## Out of scope
- Anything not listed above.

## Decisions
- Better than classic renamers. **Decision:** tested with real temp folders; no admin elevation is ever requested (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
