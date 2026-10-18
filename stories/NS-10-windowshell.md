# NS-10: Window shell: path bar, folder tree, file list

Status: Ready
Depends on: 
Area: app

## Why
A window where I pick a folder and see its files.

## Acceptance criteria
1. Given the app starts, then the window shows a folder tree on the left (drives and the special folders Desktop, Documents, Downloads, Pictures), a path bar on top and a file list on the right.
2. Given I select a folder in the tree, then the path bar shows its path and the list shows its files with columns Name, New Name, Size, Modified.
3. Given I type a valid folder path in the path bar and press Enter, then the tree and list move to it; an invalid path shows a message and keeps the previous folder.
4. Given a folder I cannot read, then the list is empty with a short message instead of a crash.
5. Given a folder with 10 000 files, then it lists in under 2 seconds and the window stays responsive (checked by hand).
6. Given the window is resized or the display scale is 150%, then nothing overlaps or is cut off (checked by hand).
7. Given a folder with hidden files, then they are not shown by default (the filter story adds the option).
8. Sub-folders are shown in the list next to files (folder icon, sorted first); they can be renamed like files.

## Out of scope
- Rule panels (NS-12), preview (NS-13).

## Decisions
- List order? **Decision:** folders first, then files, natural sort by name (`file2` before `file10`), because that is what Explorer shows and numbering must follow it (po, 2026-10-18).
- Sub-folders' contents? **Decision:** not shown; only the selected folder, with a filter option for sub-folders later (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
