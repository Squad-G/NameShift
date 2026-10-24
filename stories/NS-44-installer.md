# NS-44: Installer

Status: Draft
Depends on: NS-15, NS-38, NS-49
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
- Explorer right-click entry? **Decision (David, 2026-10-18, overriding the po's earlier choice of leaving it out):** the installer adds "Bulk Rename Here", on by default, per user; behaviour is NS-49. The compact Windows 11 menu is NS-50 (po, 2026-10-18).
- Per-user install? **Decision:** the right-click entry is always per user, so it works without admin rights even when the program is installed for all users (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: Explorer entry added to scope by David's decision (po).
- 2026-10-18: criterion 8 added: ship `NameShift.exe.config` and every library the app needs (tech-lead, from the NS-10 review).
