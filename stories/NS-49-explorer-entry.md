# NS-49: Explorer right-click entry "Bulk Rename Here": verification and README

Status: In Review
Depends on: NS-44, NS-95, NS-88, NS-89
Area: installer

## Why
Start NameShift from Explorer on a folder, or on exactly the files I selected, without browsing to them again. The window's
side is NS-88, NS-89 and NS-95 (several paths, one window, a cold start); the installer and the three keys are NS-44
(`installer/NameShift.iss`, `installer/check-registry.ps1`). This story, built after them, verifies on real Explorer how the
entry behaves, adjusts the keys if the verification needs it, and writes the README section.

## Acceptance criteria
Checked by hand on Windows 10 and 11 with the installed build (NS-44), and by `installer/check-registry.ps1` for the keys.
The PR lists what was observed, per Windows version.
1. Given the keys written by NS-44, then they are exactly these under `HKCU\Software\Classes`: `Directory\shell\NameShift`,
   `Directory\Background\shell\NameShift` and `*\shell\NameShift`, each with the default value `Bulk Rename Here`, an `Icon`
   value (`"<install folder>\NameShift.exe"`) and a `command` subkey whose default value runs the installed `NameShift.exe`
   (never `NameShiftCmd.exe`) with `"%1"` for `Directory` and `*`, and `"%V"` for `Directory\Background`. The program path
   is quoted. No other value (no `MultiSelectModel`) unless criterion 6 or 7 requires it, and then it is listed in the PR and
   the check script. If a verification changes a key, this story changes the `.iss` and `check-registry.ps1` too.
2. Given I right-click a folder, then the classic context menu shows "Bulk Rename Here" and it opens NameShift on that folder
   (one path: NS-47). On Windows 11 it is under "Show more options" (the compact menu, NS-50, was cancelled).
3. Given I right-click the empty background of an open folder, then the entry opens NameShift on that folder (`%V`; `%1` is
   empty there).
4. Given I select several files in one folder and choose the entry, with no NameShift window running and then with one
   running, then ONE window shows that folder with exactly those files selected (NS-88, NS-89, NS-95), whether Explorer
   starts one process per item or one process with all of them.
5. Given paths with spaces, non-ASCII characters, `&`, `%` and a network path, then they open correctly. Given a drive root:
   right-clicking the drive's icon in This PC is not covered (no `Drive\shell` key; adding it is a wording change for David);
   `D:\` reaches the program through the folder background (`%V`) inside the drive, and `"D:\"` (a backslash before the
   closing quote) is handled by NS-47's closing-quote rule; this is verified.
6. Given the verification of a static verb on a large selection (reported: the entry may be hidden above about 15 selected
   items), then the tech lead tests, per Windows version, two values: `MultiSelectModel` unset, and
   `MultiSelectModel=Document` (written as a string value on the three keys), with 5, 15, 16, 50 and 200+ files selected.
   Per case it records whether the entry shows and whether a burst of that many processes reaches one window with all files
   selected (NS-89: hand-over window 5 s, one pipe client at a time, 2 s read timeout). The best working value is written
   in the keys (`.iss` and check script), the result in the PR and in the README (a short sentence). If `Document` removes
   the limit, the result goes to David before the README wording is final (his acceptance of the limit, 2026-10-25, may no
   longer be needed); if it does not, David's decision stands: README text, no shell extension.
7. Given a selection of files and folders together, then whether Explorer shows the entry (the verb is in two class keys, `*`
   and `Directory`) is verified, not assumed, and the result is written in the PR. If it is hidden, it is recorded in the
   README and not promised; a fourth key (`AllFilesystemObjects`) is a wording change for David and is not added by this
   story.
8. Given the entry is removed (uninstall or the task unchecked on a re-install), then the three keys and their contents are
   gone (`check-registry.ps1`; empty parent keys may remain); nothing else is written by this story.
9. Given the README, then it has a short "Right-click in Explorer" section: what the entry does, Windows 11's "Show more
   options", Explorer's limit on many files as verified in criterion 6 and, when a limit remains, that the user opens the
   folder with the folder or folder-background entry and presses Ctrl+A in the window, and the result of criterion 7.

## Out of scope
- The compact top-level Windows 11 menu (NS-50, cancelled by David, 2026-10-25).
- Entries for other programs, or for one file type only; the `Drive` key; the fourth key for mixed selections.
- A "Send to" entry or a shell extension for selections above Explorer's limit.
- Writing the keys from the app at run time; the installer itself (NS-44); the cold-start fix (NS-95).

## Decisions
- Label and feature? **Decision (David, 2026-10-18):** NameShift gets an Explorer right-click entry labelled "Bulk Rename
  Here"; this is David's choice, recorded as given.
- Classic entry or compact Windows 11 menu first? **Decision:** classic registry entry first, because it needs no packaging
  and keeps the installer small; on Windows 11 it appears under "Show more options" (po, 2026-10-18).
- One window for many files? **Decision:** required, built as NS-88, NS-89 and NS-95 (po, 2026-10-18; 2026-10-25).
- Selection by exact files? **Decision:** yes, same as the window's own selection (NS-14), so Rename touches only what the
  user picked (po, 2026-10-18).
- Who writes the keys? **Decision:** the installer, in `[Registry]` with `uninsdeletekey`; the app never touches the registry
  (po, 2026-10-25). NS-44 writes them; this story verifies and adjusts.
- `%1` or `%V`? **Decision:** `"%1"` on `Directory` and `*`, `"%V"` on `Directory\Background`; `%L` and `%W` are not used (po,
  2026-10-25).
- A very large selection? **Decision (David, 2026-10-25):** the limit of Explorer is accepted for v1. The README explains that
  for a big selection the user opens the folder with the folder or folder-background entry and selects in the window
  (Ctrl+A). No shell extension. Criterion 6 may show the limit goes away with `MultiSelectModel=Document`; then David is
  told before the wording is final (po, 2026-10-25).
- Story order? **Decision (lead, 2026-10-25):** NS-95 (cold start), then NS-44, then this story; it depends on both (the
  circle of the first draft is removed).
- Empty parent keys after uninstall? **Decision:** accepted: `uninsdeletekey` removes NameShift's keys, not the shared parent
  keys that other programs may use (po, 2026-10-25).

## Log
- 2026-10-18: created (po).
- 2026-10-24: stays Draft: too big for one story (po).
- 2026-10-25: split: the window's part is NS-88 and NS-89; this story is the registry entry (po).
- 2026-10-25: Ready (po).
- 2026-10-25: PR #107 review: now built after NS-44 and NS-95 and covers the Explorer verification (Document test at 200+
  files, mixed selections, drive roots), the README section and any key adjustment; criterion 4 needs NS-95 (po).
- 2026-10-25: In Progress (builder, phase A).
- 2026-10-25: In Review (builder, phase A): criterion 1 read against the .iss and the check script (Removed: all PASS); `&` test rows; README section with a TODO for the limit and mixed-selection sentences; setup built (2.29 MB). Waits on David's observations (criteria 2 to 8) before phase B; the PR stays a draft.
