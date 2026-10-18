# NS-37: Remember last settings

Status: Draft
Depends on: NS-36
Area: app

## Why
The app opens as I left it.

## Acceptance criteria
1. Given I close and reopen, then the window size, position, splitter positions, last folder and panel values come back.
2. Given the last folder no longer exists, then the app opens in Documents.
3. Given the settings file is damaged, then defaults are used.
4. Given the window position is on a screen that is gone, then it opens on the primary screen.
5. Given a setting `start empty`, then the app starts with all rules off (kept simple: a menu item `Reset all`).

## Out of scope
- Anything not listed above.

## Decisions
- Remember rules or start clean? **Decision:** remember them, with Reset to clear, because repeat jobs are common (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
