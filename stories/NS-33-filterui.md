# NS-33: Filters panel and sub-folders option

Status: Draft
Depends on: NS-32, NS-51
Area: app

## Why
Filter the list from the window and include sub-folders.

## Acceptance criteria
1. Given the filter panel, then every filter option is editable and the file list updates live.
2. Given the sub-folders option on, then files of all sub-folders are listed with their relative folder shown, and renamed in place.
3. Given the folder is huge, then listing sub-folders stays cancelable and the window responsive (checked by hand).
4. Given the app starts or the filter panel is reset, then the sub-folders option is off.

## Out of scope
- Anything not listed above.

## Decisions
- Cycles through junctions? **Decision:** reparse points (junctions, links) are never followed, to avoid loops and surprises (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
