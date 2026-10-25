# NS-44: Installer

Status: Ready
Depends on: NS-15, NS-38, NS-49
Order: the very last story before the v1 release (David, 2026-10-25). NS-49 is built in the same round.
Area: installer

## Why
Install NameShift like any Windows app.

## Acceptance criteria
Files: `installer/NameShift.iss` (Inno Setup 6), `installer/build-installer.sh` (Git Bash, like David's tools), the
`installer/README.md` (how to build and what David does by hand) and `installer/check-registry.ps1` (NS-49). The installer
build is a separate manual step: `dotnet build` and `dotnet test` of the repo never need Inno Setup. Nothing in `tools/` or
`docs/RELEASING.md` is changed by this story.
1. Given `installer/build-installer.sh`, then it runs `dotnet build -c Release`, finds `ISCC.exe` (the standard install
   folders, or the PATH; a clear message if missing) and produces `installer/Output/NameShift-Setup-<version>.exe` of at most
   3 MB (LZMA solid). `installer/Output/` is ignored by git, so the working tree stays clean for `tools/release.sh`.
2. Given install, then it is per user only: no admin rights and no UAC prompt (`PrivilegesRequired=lowest`), program files in
   `%LocalAppData%\Programs\NameShift` (the user may choose another folder), a Start menu shortcut, and an optional desktop
   shortcut (off by default).
3. Given reinstall or a newer version over an old one, then it upgrades in place (fixed `AppId`, generated once and never
   changed), keeps the user's data and keeps the choices of the tasks. If `NameShift.exe` is running, setup asks to close it
   (Inno's Restart Manager closing) and never overwrites a file in use silently.
4. Given uninstall, then the program files, shortcuts, the Explorer keys (criterion 7) and the PATH entry (criterion 6) are
   removed, and the user's settings, presets and history are kept (`%LocalAppData%\NameShift` and
   `%AppData%\NameShift`). An interactive uninstall asks once: `Also delete your NameShift settings, presets and history?`
   with No as the default and a plain warning that Revert of past renames is lost if history is deleted; a silent uninstall
   (`/SILENT`, `/VERYSILENT`) keeps them. Only those two NameShift folders are ever deleted, and only on Yes.
5. Given .NET Framework 4.8 is missing (older Windows 10), then setup says so in one clear message (with the name of what is
   missing) and stops before copying anything. The check reads `HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full`
   value `Release` >= 528040.
6. Given an optional task `Add NameShift to PATH` (off by default), then the install folder is added once to the user's
   `HKCU\Environment` `Path` (no duplicate on reinstall; other entries untouched; value type kept), so `NameShiftCmd` runs
   from a new cmd or PowerShell window; uninstall removes exactly that entry.
7. Given the task `Add "Bulk Rename Here" to the Explorer right-click menu` (a checkbox, on by default), then the entry of
   NS-49 (its three keys, in the `[Registry]` section with `uninsdeletekey`, for the current user) is written; unchecked,
   nothing is written; uninstall removes every key it added.
8. Given install, then the program folder holds `NameShift.exe`, `NameShiftCmd.exe`, `NameShift.exe.config` (it carries the
   PerMonitorV2 DPI setting; without it the window blurs at 150% scale), `LICENSE` and `README.md`, and every library the two
   programs need: the script copies the whole Release output of `NameShift.App` and `NameShift.Cmd.Exe` (net48), without
   `.pdb` and `.xml` files, and a file present in both is installed once. A check in the build script fails if one of
   `NameShift.Core.dll`, `NameShift.Disk.dll`, `NameShift.Ui.dll` or `NameShift.Cmd.dll` is missing from the result. After
   install, `NameShift.exe` starts and `NameShiftCmd.exe --version` runs (by hand).
9. Given the version, then it comes from the built `NameShift.exe` (the `<Version>` set by `tools/bump-version.sh`, single
   source: the script reads it from the file, nothing is typed in the `.iss`). The installer is unsigned (no certificate);
   the README says Windows SmartScreen may warn for an unsigned download and how to continue.
10. Given the setup window, then its language is English and it shows "NameShift" with the MIT licence page (`LICENSE`).
    It sets the Add/Remove Programs entry (name, version, publisher `Squad-G`, per user) and a `/DIR`, `/SILENT` and `/TASKS`
    command line works.
11. Given publishing, then David does it by hand: he builds with `installer/build-installer.sh` after `tools/release.sh`, then
    attaches the file to a GitHub release. `installer/README.md` says so in five lines at most. The PR tells David that
    `docs/RELEASING.md` ("Coming later") can be updated by him; the story does not edit it.
12. By hand, written in the PR, on a clean Windows 10 or 11 user: install with all defaults; install with each task
    changed; upgrade over an older build; uninstall with No and with Yes; silent uninstall; `check-registry.ps1` after install
    and after uninstall; PATH before and after.

## Out of scope
- Code signing, Store, auto-update, a per-machine (all users) install, other languages of setup.
- Changing `tools/release.sh`, `tools/bump-version.sh` or `docs/RELEASING.md`.
- A version stamped on `NameShiftCmd.exe`.

## Decisions
- PATH default? **Decision:** off, because it changes the environment and most users never need it (po, 2026-10-18).
- Explorer right-click entry? **Decision (David, 2026-10-18, overriding the po's earlier choice of leaving it out):** the
  installer adds "Bulk Rename Here", on by default, per user; behaviour is NS-49. The compact Windows 11 menu (NS-50) was
  cancelled by David on 2026-10-25: the classic entry is the only one.
- Keys by the installer or by the app? **Decision:** the installer, in `[Registry]` (po, 2026-10-25; NS-49).
- Per-user or per-machine? **Decision:** per user only for v1, superseding the earlier "optional per-machine" wording
  (po, 2026-10-25). Reasons: no admin and no UAC; an elevated install writes `HKCU` of the elevating account, which may not be
  the user's, so the right-click entry and PATH would land in the wrong profile; the program writes only to the user's own
  folders anyway. "Program Files" is not used.
- Settings and history on uninstall? **Decision:** kept by default; one question with No as default, silent keeps (po,
  2026-10-25): never lose the user's undo history by accident.
- When is the installer built? **Decision (David, 2026-10-24/25):** at the very end, just before the v1 release, so it ships
  the final set of files and the Explorer entry in one go; NS-49 is built in the same round (po, 2026-10-25).
- How does it fit David's release? **Decision:** the release scripts stay as they are and know nothing of the installer;
  the installer has its own script and an ignored output folder, so a clean tree is kept for `tools/release.sh` (po,
  2026-10-25).
- Version of `NameShiftCmd.exe`? **Decision (David, 2026-10-25):** the release scripts are left alone: nothing stamps a
  version on `NameShiftCmd.exe`. The installer's version comes from `NameShift.exe` (criterion 9), and
  `NameShiftCmd --version` prints what it prints today (po, 2026-10-25).
- Single-instance mutex and Inno's `AppMutex`? **Decision:** not used: the app's mutex name has the user SID; Restart Manager
  closing (criterion 3) is enough (po, 2026-10-25).

## Log
- 2026-10-18: created (po).
- 2026-10-18: Explorer entry added to scope by David's decision (po).
- 2026-10-18: criterion 8 added: ship `NameShift.exe.config` and every library the app needs (tech-lead, from the NS-10 review).
- 2026-10-24: stays Draft: waits on NS-49 (criterion 7) and its split (po).
- 2026-10-25: David's decisions recorded: the installer is the last story, built just before v1; NameShiftCmd gets no version
  stamping from the release scripts (po).
- 2026-10-25: re-checked against RELEASING.md, tools/release.sh and the repo: per-user only, build script and output folder
  apart from the release tooling, README and LICENSE shipped, uninstall asks about user data, .NET check, upgrade and tests by
  hand added. Ready, built with NS-49 (po).
