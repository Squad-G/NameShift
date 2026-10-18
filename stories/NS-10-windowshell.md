# NS-10: Window shell: path bar and folder tree

Status: In Progress
Depends on:
Area: app

## Why
A window where I pick a folder. The file list that shows its contents is NS-51.

## Acceptance criteria
1. Given the app starts, then the window shows a folder tree on the left (drives and the special folders Desktop, Documents, Downloads, Pictures), a path bar on top and an area on the right reserved for the file list (NS-51) and the rule panels below it (NS-11).
2. Given I select a folder in the tree, then the path bar shows its path and the window raises a "folder changed" event carrying that path (NS-51 listens to it).
3. Given I type a valid folder path in the path bar and press Enter, then the tree moves to it and the same event is raised; an invalid path shows a message and keeps the previous folder.
4. Given a drive that is not ready (empty card reader) or a folder I cannot read, then the tree shows a short message instead of a crash.
5. Given the window is resized or the display scale is 150%, then nothing overlaps or is cut off (checked by hand).
6. Given the window closes and reopens, then it starts again on the Documents folder (remembering the last folder is NS-37).

## Out of scope
- The file list (NS-51), rule panels (NS-11), preview (NS-13).

## Decisions
- Sub-folders' contents in the list? **Decision:** not shown; only the selected folder, with a filter option for sub-folders later (po, 2026-10-18).
- Split from the file list because tree, path bar and list are each a full PR of work (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: file list moved to NS-51 after review (po).
- 2026-10-18: In Progress (builder).
