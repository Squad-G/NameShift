# NS-10: Window shell: path bar and folder tree

Status: Done
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
- New library `NameShift.Ui` (netstandard2.0, no WinForms) holds the window logic and all strings; the App has thin controls (tech-lead, 2026-10-18).
- Tests in new `NameShift.Ui.Tests` (net10.0 and net48); the App has none, it is checked by hand (tech-lead, 2026-10-18).
- Pattern for later window stories is written in `CLAUDE.md`, Window code (tech-lead, 2026-10-18).
- PerMonitorV2 through App.config plus a Windows 10 compatibility manifest (tech-lead, 2026-10-18).
- Tree roots: Desktop, Documents, Downloads, Pictures, then drives; no call that touches a drive while building roots (tech-lead, 2026-10-18).
- Folders with the Hidden attribute are not shown; System alone does not hide a folder (tech-lead default, 2026-10-18; po may overrule).
- Children sort with Explorer's natural order (`NaturalNameComparer`), reused by NS-51 for the list (tech-lead default, 2026-10-18; po may overrule).
- Start folder is Documents on every start, else the system drive root; nothing is persisted (tech-lead default, 2026-10-18; po may overrule).
- Path bar: Enter navigates; Escape or leaving restores the current path; an invalid entry keeps the folder, keeps the text selected and shows a status message; the existence check runs off the UI thread and a newer navigation supersedes it (tech-lead, 2026-10-18).
- One `FolderNavigator` raises `FolderChanged` only on a real change, so tree and path bar stay in sync without loops (tech-lead, 2026-10-18).
- A typed path opens under the deepest root that contains it; if it is not reachable in the tree, navigation still happens and the selection clears (tech-lead, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: file list moved to NS-51 after review (po).
- 2026-10-18: In Progress (builder).
- 2026-10-18: In Review (builder): NameShift.Ui library + tests; tree lazy-loads off the UI thread; PerMonitorV2.
- 2026-10-18: Done (tech-lead): merged after review round 2 APPROVE. The manual checks the builder could not run (clicking in the tree, typing in the path bar, expanding a node after a hidden-path reveal, a folder with access denied, 150% scale and moving the window between monitors) are pending David's check.
