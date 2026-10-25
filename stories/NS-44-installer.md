# NS-44: Installer

Status: Draft
Depends on: NS-15, NS-38, NS-49
Order: the very last story before the v1 release (David, 2026-10-25). NS-49 is built in the same round.
Area: installer

## Why
Install NameShift like any Windows app.

## Acceptance criteria
1. Given the Inno Setup script, then a build produces `NameShift-Setup-<version>.exe` of at most 3 MB.
2. Given install, then files go to Program Files, a Start menu shortcut is made and an optional desktop shortcut.
3. Given the user has no admin rights, then it offers a per-user install.
4. Given uninstall, then program files and shortcuts are removed and the user's presets and history are kept unless the user chooses to delete them.
5. Given .NET Framework 4.8 is missing (older Windows), then the installer says so clearly and stops.
6. Given an optional task `Add NameShift to PATH` (off by default, per user or per machine to match the install), then `NameShiftCmd` (the console program, NS-38) runs from a new cmd or PowerShell window, and uninstall removes the PATH entry.
7. Given the task `Add "Bulk Rename Here" to the Explorer right-click menu` (a checkbox, on by default), then the entry of NS-49 is registered for the current user only (no admin rights), appears for folders, folder backgrounds and file selections, and uninstall removes every key it added; unchecked, nothing is registered.
8. Given install, then the program folder holds `NameShift.exe`, `NameShiftCmd.exe` (NS-38), `NameShift.exe.config` (it carries the PerMonitorV2 DPI setting; without it the window blurs at 150% scale) and every library the app needs (`NameShift.Core.dll`, `NameShift.Disk.dll`, `NameShift.Ui.dll`).
9. The installer is unsigned for now; the version comes from the app (single source). Publishing the installer is done by David (docs/RELEASING.md).

## Out of scope
- Code signing, Store, auto-update.

## Decisions
- PATH default? **Decision:** off, because it changes the environment and most users never need it (po, 2026-10-18).
- Explorer right-click entry? **Decision (David, 2026-10-18, overriding the po's earlier choice of leaving it out):** the installer adds "Bulk Rename Here", on by default, per user; behaviour is NS-49. The compact Windows 11 menu (NS-50) was cancelled by David on 2026-10-25: the classic entry is the only one.
- Per-user install? **Decision:** the right-click entry is always per user, so it works without admin rights even when the program is installed for all users (po, 2026-10-18).
- When is the installer built? **Decision (David, 2026-10-24/25):** at the very end, just before the v1 release, so it ships the final set of files and the Explorer entry in one go. This closes the open question of 2026-10-24 (ship without the entry or wait): it waits, and NS-49 is built in the same round (po, 2026-10-25).
- Version of `NameShiftCmd.exe`? **Decision (David, 2026-10-25):** the release scripts are left alone: nothing stamps a version on `NameShiftCmd.exe`. The installer's version comes from `NameShift.exe` (criterion 9), and `NameShiftCmd --version` prints what it prints today (po, 2026-10-25).

## Log
- 2026-10-18: created (po).
- 2026-10-18: Explorer entry added to scope by David's decision (po).
- 2026-10-18: criterion 8 added: ship `NameShift.exe.config` and every library the app needs (tech-lead, from the NS-10 review).
- 2026-10-24: stays Draft: waits on NS-49 (criterion 7) and its split. Open question for David: ship the installer
  first without the right-click entry (criteria 1 to 6, 8 and 9), or wait for NS-49? po default: wait, the entry was
  his explicit request (po).
- 2026-10-25: David's decisions recorded: the installer is the last story, built just before v1; the question above is
  answered (wait; NS-49 is built in the same round, after NS-88 and NS-89 are merged); NameShiftCmd gets no version
  stamping from the release scripts. Stays Draft until then; no open question left (po).
