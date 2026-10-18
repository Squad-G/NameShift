# NS-51: File list with folders and natural sort

Status: In Progress
Depends on: NS-10
Area: app

## Why
See the files and folders of the chosen folder, ready for the preview column (NS-13).

## Acceptance criteria
1. Given the folder changes (NS-10), then the list shows its entries with columns Name, New Name (empty until NS-13), Size and Modified.
2. Given a folder with files and sub-folders, then sub-folders are shown in the list next to the files (folder icon, size empty) and can be renamed like files.
3. Given the list order, then folders come first, then files, each sorted by natural order of name, case-insensitive (`file2` before `file10`).
4. Given a folder with hidden or system files, then they are not shown by default (the filter story NS-32/NS-33 adds the option).
5. Given a folder I cannot read, then the list is empty with a short message instead of a crash.
6. Given a folder with 10 000 files, then it lists in under 2 seconds and the window stays responsive (checked by hand).
7. Given the list is built, then each row is an item as defined in NS-1 (name, folder path, kind, attributes, size, dates, index).

## Out of scope
- Selection (NS-14), preview (NS-13), column sorting by click (NS-46).

## Decisions
- List order? **Decision:** folders first, then files, natural sort, because that is what Explorer shows and numbering will follow it (po, 2026-10-18).

## Log
- 2026-10-18: created, split from NS-10 (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress (builder).
