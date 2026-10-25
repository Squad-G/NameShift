# NS-49: Explorer right-click entry "Bulk Rename Here": the registry keys

Status: Ready
Depends on: NS-88, NS-89
Area: installer

## Why
Start NameShift from Explorer on a folder, or on exactly the files I selected, without browsing to them again. The window's
side (several paths, one window) is NS-88 and NS-89, both Done; this story is the entry the installer writes. It is built
together with NS-44 (one PR round, at the end, before v1); the keys are NS-44's `[Registry]` section.

## Acceptance criteria
The entry is written by the installer (NS-44 criterion 7) for the current user only. Checked by hand on Windows 10 and 11
with the installed build; the key list is checked by a script (`installer/check-registry.ps1`, PowerShell, read-only) that
reads the registry and prints PASS or FAIL per line, run after install and after uninstall.
1. Given the keys, then the installer writes only these, under `HKCU\Software\Classes`: `Directory\shell\NameShift`,
   `Directory\Background\shell\NameShift` and `*\shell\NameShift`, each with the default value `Bulk Rename Here`, an `Icon`
   value (`"<install folder>\NameShift.exe"`) and a `command` subkey whose default value runs the installed `NameShift.exe`
   (never `NameShiftCmd.exe`) with `"%1"` for `Directory` and `*`, and `"%V"` for `Directory\Background`. The program path is
   quoted. Nothing else is written (no `MultiSelectModel` or other value unless criterion 6 requires it, and then it is
   listed in the PR and the check script). No admin rights are needed (NS-44 installs per user only).
2. Given I right-click a folder, then the classic context menu shows "Bulk Rename Here" and it opens NameShift on that folder
   (one path: NS-47). On Windows 11 it is under "Show more options" (the compact menu, NS-50, was cancelled).
3. Given I right-click the empty background of an open folder, then the entry opens NameShift on that folder (`%V` is the
   folder shown; it is the only variable that is right there, `%1` is empty).
4. Given I select several files (or files and folders) in one folder and choose the entry, then ONE NameShift window shows
   that folder with exactly those items selected (NS-88, NS-89), whether Explorer starts one process per item, each with one
   `"%1"` (the gathering of NS-89), or one process with all of them.
5. Given paths with spaces, non-ASCII characters, `&`, `%`, a drive root (`D:\`: `"D:\"` ends in a backslash before the
   closing quote, which NS-47's closing-quote rule already handles) and a network path, then they open correctly.
6. Given the tech lead verifies on Windows 10 and 11 how Explorer treats this static verb on a large selection (reported: the
   entry may be hidden above about 15 selected items), and whether leaving `MultiSelectModel` unset, or setting it, gives the
   best result for criterion 4, then the choice and the observed limit are written in the PR and in the README (a short
   sentence), and the keys follow what is verified. The default is to leave it unset unless a test shows a problem.
7. Given the entry is removed (uninstall or the task unchecked on a re-install), then no key, value or leftover of these keys
   remains (`check-registry.ps1`, run on a clean machine); nothing outside the three keys and the install folder was written
   by this story (the PATH task and the uninstall registration belong to NS-44).
8. Given NS-44 is built, then the task `Add "Bulk Rename Here" to the Explorer right-click menu` is on by default and
   unchecked it writes nothing.
9. Given a user whose own key already exists (another tool also called its entry `NameShift`: unlikely), then the installer
   replaces it, and uninstall deletes it (the keys are NameShift's by name; nothing else is searched or merged).
10. Given the README, then it has a short "Right-click in Explorer" section: what the entry does, Windows 11's "Show more
    options", and that for a big selection (Explorer may not show the entry above about 15 files) the user opens the folder
    with the folder or folder-background entry and presses Ctrl+A in the window.

## Out of scope
- The compact top-level Windows 11 menu (NS-50, cancelled by David, 2026-10-25).
- Entries for other programs, or for one file type only.
- A "Send to" entry or a shell extension for selections above Explorer's limit.
- Writing the keys from the app at run time.

## Decisions
- Label and feature? **Decision (David, 2026-10-18):** NameShift gets an Explorer right-click entry labelled "Bulk Rename
  Here"; this is David's choice, recorded as given.
- Classic entry or compact Windows 11 menu first? **Decision:** classic registry entry first, because it needs no packaging
  and keeps the installer small; on Windows 11 it appears under "Show more options". The top-level menu is NS-50 (po,
  2026-10-18).
- One window for many files? **Decision:** required, and built as NS-88 and NS-89 (po, 2026-10-18; 2026-10-25).
- Selection by exact files? **Decision:** yes, same as the window's own selection (NS-14), so Rename touches only what the
  user picked (po, 2026-10-18).
- Who writes the keys? **Decision:** the installer, in the Inno `[Registry]` section with `uninsdeletekey`, not the app at
  run time: Inno removes exactly what it wrote on uninstall, and the app never touches the registry (po, 2026-10-25).
- `%1` or `%V`? **Decision:** `"%1"` on `Directory` and `*` (the clicked item; for a selection Explorer gives each item as
  `%1`, which NS-88 and NS-89 turn into one window), `"%V"` on `Directory\Background` (the open folder; `%1` is empty
  there). `%L` and `%W` are not used (po, 2026-10-25).
- A very large selection? **Decision (David, 2026-10-25):** the limit of Explorer is accepted for v1. The README explains that
  for a big selection the user opens the folder with the folder or folder-background entry and selects in the window
  (Ctrl+A). No shell extension, so the installer stays small.
- Story order? **Decision:** built with the installer at the very end, before v1 (David, 2026-10-24/25).

## Log
- 2026-10-18: created (po).
- 2026-10-24: stays Draft: too big for one story (po).
- 2026-10-25: split: the window's part is NS-88 (several paths, selection) and NS-89 (one window); this story is now the
  registry entry, built with the installer (NS-44). The selection limit of static verbs is recorded for the tech lead to
  verify (po).
- 2026-10-25: NS-88 and NS-89 are merged and re-checked: `%1` and `%V` fixed, keys by the installer, check script and README
  section added (criteria 5, 9, 10). Ready, built in the same round as NS-44 (po).
