# NS-49: Explorer right-click entry "Bulk Rename Here": the registry keys

Status: Draft
Depends on: NS-88, NS-89
Area: installer

## Why
Start NameShift from Explorer on a folder, or on exactly the files I selected, without browsing to them again. The window's
side (several paths, one window) is NS-88 and NS-89; this story is the entry the installer writes. It is built in the
installer round (NS-44), at the end, so it stays Draft until then.

## Acceptance criteria
The entry is written by the installer (NS-44 criterion 7) for the current user only. Checked by hand on Windows 10 and 11
with the installed build; the key list is checked by a script that reads the registry after install and after uninstall.
1. Given the keys, then the installer writes only these, under `HKCU\Software\Classes`: `Directory\shell\NameShift`,
   `Directory\Background\shell\NameShift` and `*\shell\NameShift`, each with the default value `Bulk Rename Here`, an `Icon`
   value (the installed `NameShift.exe`) and a `command` subkey that runs the installed `NameShift.exe` (never
   `NameShiftCmd.exe`) with `"%1"` (folders and files) or `"%V"` (the folder background). No admin rights are needed, also when
   the program itself is installed for all users.
2. Given I right-click a folder, then the classic context menu shows "Bulk Rename Here" and it opens NameShift on that folder.
   On Windows 11 it is under "Show more options" (the compact menu, NS-50, was cancelled).
3. Given I right-click the empty background of an open folder, then the entry opens NameShift on that folder.
4. Given I select several files (or files and folders) in one folder and choose the entry, then ONE NameShift window shows
   that folder with exactly those items selected (NS-88, NS-89), whichever way Explorer starts the program.
5. Given paths with spaces, non-ASCII characters or a drive root, then they are handled.
6. Given the tech lead verifies how Explorer treats a static verb on a large selection (reported: the entry may not show
   above about 15 selected items; to be checked on Windows 10 and 11) and records the multi-select setting chosen, then the
   result is written in the PR and in the README (a short sentence), and the story's behaviour follows what is verified.
7. Given the entry is removed (uninstall or the task unchecked), then no key, value or leftover of these keys remains
   (the key check described above, run on a clean machine); nothing outside the three keys and the install folder was
   written.
8. Given NS-44 is built, then the task `Add "Bulk Rename Here" to the Explorer right-click menu` is on by default and
   unchecked it writes nothing.

## Out of scope
- The compact top-level Windows 11 menu (NS-50, cancelled by David, 2026-10-25).
- Entries for other programs, or for one file type only.
- A "Send to" entry or a shell extension for selections above Explorer's limit.

## Decisions
- Label and feature? **Decision (David, 2026-10-18):** NameShift gets an Explorer right-click entry labelled "Bulk Rename
  Here"; this is David's choice, recorded as given.
- Classic entry or compact Windows 11 menu first? **Decision:** classic registry entry first, because it needs no packaging
  and keeps the installer small; on Windows 11 it appears under "Show more options". The top-level menu is NS-50 (po,
  2026-10-18).
- One window for many files? **Decision:** required, and built first as NS-88 and NS-89 (po, 2026-10-18; 2026-10-25).
- Selection by exact files? **Decision:** yes, same as the window's own selection (NS-14), so Rename touches only what the
  user picked (po, 2026-10-18).
- Setup of the entry? **Decision:** done by the installer (NS-44), not by the app at run time (po, 2026-10-18).
- A very large selection? **Decision:** the earlier wish was "hundreds of files"; the classic entry cannot promise it if
  Explorer hides a static verb above its limit. For v1 the limit is accepted and written in the README: open the folder with
  the folder entry (or the background entry) and select in the window (Ctrl+A). No shell extension, so the installer stays
  small (po, 2026-10-25).
- Explorer's limit on many selected files? **Decision (David, 2026-10-25):** accepted for v1. The README explains that for a big selection the user opens the folder with the folder or folder-background entry and selects in the window; no shell extension.
- Story order? **Decision:** NS-88 and NS-89 are built now; this story is built with the installer at the very end, before
  v1 (David, 2026-10-25, for NS-44; applied here by the po).

## Log
- 2026-10-18: created (po).
- 2026-10-24: stays Draft: too big for one story (po).
- 2026-10-25: split: the window's part is NS-88 (several paths, selection) and NS-89 (one window); this story is now the
  registry entry, built with the installer (NS-44). The selection limit of static verbs is recorded for the tech lead to
  verify (po).
