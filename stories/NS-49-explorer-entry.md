# NS-49: Explorer right-click entry "Bulk Rename Here"

Status: Draft
Depends on: NS-47, NS-14
Area: app

## Why
Start NameShift from Explorer on a folder, or on exactly the files I selected, without browsing to them again.

## Acceptance criteria
1. Given I right-click a folder, then the classic context menu shows "Bulk Rename Here" and choosing it opens NameShift on that folder.
2. Given I right-click the empty background inside an open folder, then the same entry opens NameShift on that folder.
3. Given I select several files in Explorer and choose the entry, then ONE NameShift window opens on their common folder with exactly those files selected (never one window per file, even for hundreds of files).
4. Given I select files and folders together, then they are all selected in the window.
5. Given the entry is used while NameShift is already open, then the running window is brought to the front and shows the new folder and selection (one window at all times); it simply switches, because nothing is lost until Rename is pressed.
6. Given paths with spaces, non-ASCII characters or a drive root, then they are handled.
7. Given a selection too large for a command line (over about 8000 characters), then the window still gets all files (the paths are passed another way, chosen by the tech lead).
8. Given the entry is registered per user, then no admin rights are needed and nothing outside the current user's registry and the install folder is changed.
9. Given it is removed, then no key, value or leftover remains (checked by hand on a clean machine).

## Out of scope
- The compact top-level Windows 11 menu (NS-50).
- Entries for other programs, or file-type specific entries.

## Decisions
- Label and feature? **Decision (David, 2026-10-18):** NameShift gets an Explorer right-click entry labelled "Bulk Rename Here"; this is David's choice, recorded as given.
- Classic entry or compact Windows 11 menu first? **Decision:** classic registry entry first, because it needs no packaging and keeps the installer small; on Windows 11 it appears under "Show more options". The top-level menu is NS-50 (po, 2026-10-18).
- One window for many files? **Decision:** required. Explorer starts one process per selected file with a plain verb, so the app must be single-instance and gather the paths (named mutex plus a pipe, or the shell's multi-select model); the mechanism is for the tech lead (po, 2026-10-18).
- Selection by exact files? **Decision:** yes, same as the window's own selection (NS-14), so Rename touches only what the user picked (po, 2026-10-18).
- Setup of the entry? **Decision:** done by the installer (NS-44), not by the app at run time (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-24: stays Draft: too big for one story (single-instance window and the hand-off of many paths, then the
  selection in the window, then the registry entry the installer writes). To split in a later round, after the filters
  and presets are in (po).
