# NS-52: Conflict display

Status: Ready
Depends on: NS-13, NS-5, NS-6, NS-7
Area: app

## Why
Know which names cannot be used, and why, before pressing Rename.

## Acceptance criteria
1. Given any conflict flag from NS-5, NS-6 or NS-7 (InvalidCharacter, ReservedName, TrailingDotOrSpace, EmptyName, NameTooLong, DuplicateTarget, TargetExists, PathTooLong), then the row is marked red and a Status column states the reason in plain words.
2. Given conflicts, then a summary under the list says `N conflicts` (and nothing when there are none).
3. Given a conflict, then it is also shown by an icon or text, never by colour alone (accessibility).
4. Given a `TargetExists` flag, then it is shown as NS-6 computes it (where the existing names come from is defined once, in NS-6:3).
5. Given a file that is not selected (NS-14), then its Status is empty; it can still cause `TargetExists` on a selected file.
6. Given a conflict whose detail contains a control character (NS-5 returns it raw, e.g. a tab), then it is shown escaped (e.g. `U+0001`), never raw.

## Out of scope
- Disabling the Rename button (NS-15 owns that).
- Plain-sentence refinements of the messages (NS-46).

## Decisions
- Where does "Rename disabled on conflicts" live? **Decision:** only in NS-15, so it is written once (po, 2026-10-18).

## Log
- 2026-10-18: created, split from NS-13 (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: criterion 6 added, control characters in a conflict's detail are shown escaped (tech-lead, from the NS-5 review).
