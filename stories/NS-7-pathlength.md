# NS-7: Path length check

Status: Ready
Depends on: NS-1
Area: engine

## Why
Warn before a rename would produce a path Windows cannot handle.

## Acceptance criteria
1. Given a folder path of 200 characters and a new name of 60, then the full path is 261 and the item is flagged `PathTooLong` (limit 259 characters plus terminator, i.e. full path longer than 259 is flagged).
2. Given a full path of exactly 259 characters, then it is not flagged.
3. Given the path already exceeds the limit and the name is unchanged, then it is not flagged.
4. Given a new name shorter than the old one in an over-long folder, then it is still flagged only if the resulting full path exceeds the limit.

## Out of scope
- Long-path (`\\?\`) support (can be a later story).

## Decisions
- Limit value? **Decision:** 259 characters, the classic limit, because it works in every Windows program including Explorer (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
