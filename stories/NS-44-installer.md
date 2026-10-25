# NS-44: Installer

Status: In Progress
Depends on: NS-15, NS-38
Order: built first in the last round before the v1 release (David, 2026-10-24/25); NS-49 follows and depends on it.
Area: installer

## Why
Install NameShift like any Windows app.

## Acceptance criteria
Files: `installer/NameShift.iss` (Inno Setup 6), `installer/build-installer.sh` (Git Bash, like David's tools),
`installer/README.md` (how to build and what David does by hand), `installer/check-registry.ps1`, `.gitignore` and the root
`README.md` (its Layout line). The installer build is a separate manual step: `dotnet build` and `dotnet test` of the repo
never need Inno Setup. Nothing in `tools/` or `docs/RELEASING.md` is changed by this story.
1. Given `installer/build-installer.sh`, then it runs `dotnet build -c Release`, finds `ISCC.exe` (the standard install
   folders, or the PATH; a clear message if missing) and produces `installer/Output/NameShift-Setup-<version>.exe` of at most
   3 MB (LZMA solid). The story adds the line `installer/Output/` to `.gitignore`: after a build, `git status --porcelain`
   is empty, so `tools/release.sh` still runs.
2. Given install, then it is per user only: no admin rights and no UAC prompt (`PrivilegesRequired=lowest`), program
   files in `%LocalAppData%\Programs\NameShift` (the user may choose another folder, but not `%LocalAppData%\NameShift`
   or `%AppData%\NameShift`, which hold the user's data, nor a folder inside either: setup refuses them, because Yes on
   the uninstall question deletes the data folders), a Start menu shortcut, and an optional desktop shortcut (off by
   default).
3. Given reinstall or a newer version over an old one, then it upgrades in place (fixed `AppId`, generated once and never
   changed), keeps the user's data and the choices of the tasks. If `NameShift.exe` is running, an interactive setup asks to
   close it and a silent one (`/SILENT`, `/VERYSILENT`) closes it (Inno's Restart Manager closing); a file in use is never
   overwritten silently.
4. Given uninstall, then the program files, shortcuts, the Explorer keys (criterion 7) and the PATH entry (criterion 6) are
   removed, and the user's settings, presets and history are kept (`%LocalAppData%\NameShift` and `%AppData%\NameShift`).
   An interactive uninstall asks once: `Also delete your NameShift settings, presets and history?`, with No as the default
   button (`MB_DEFBUTTON2`) and a plain warning that Revert of past renames is lost if history is deleted. A silent
   uninstall (`UninstallSilent`) does not ask and keeps them. Only those two NameShift folders are ever deleted, and only
   on Yes.
5. Given .NET Framework 4.8 is missing (older Windows 10), then setup says so in one clear message (with the name of what is
   missing), copies nothing and exits with a non-zero code, also with `/VERYSILENT` (the message box may still show unless
   `/SUPPRESSMSGBOXES`). The check reads `HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full` value `Release` >= 528040.
6. Given an optional task `Add NameShift to PATH` (off by default), then the install folder is added once to the user's
   `HKCU\Environment` `Path` (no duplicate on reinstall; other entries untouched). The value is written back with the kind it
   had: a `REG_EXPAND_SZ` Path stays expandable (`RegWriteExpandStringValue`), so entries such as `%USERPROFILE%\bin` are not
   frozen. `ChangesEnvironment=yes` is set. `NameShiftCmd` then runs from a new cmd or PowerShell window; uninstall removes
   exactly that entry. Tested by hand with a Path that holds `%USERPROFILE%` entries, before and after install and uninstall.
7. Given the task `Add "Bulk Rename Here" to the Explorer right-click menu` (a checkbox, on by default), then the three keys
   of NS-49 are written in the `[Registry]` section (`Root: HKCU`, `uninsdeletekey`, a quote inside a value doubled), for the
   current user; unchecked, nothing is written; uninstall removes them. This story writes the keys and the read-only
   `installer/check-registry.ps1` (PASS or FAIL per line; run with `powershell -ExecutionPolicy Bypass -File`, because the
   default policy blocks an unsigned script) that tests: the three keys exist after install with the exact values of NS-49
   criterion 1, and after uninstall the three keys and their contents are gone (empty parent keys may remain; the script
   tests only this). NS-49 verifies and may adjust the keys (how Explorer behaves).
8. Given install, then the program folder holds `NameShift.exe`, `NameShiftCmd.exe`, `NameShift.exe.config` (it carries the
   PerMonitorV2 DPI setting; without it the window blurs at 150% scale), `LICENSE` and `README.md`, and every library the two
   programs need: the script copies the whole Release output of `NameShift.App` and `NameShift.Cmd.Exe` (net48), without
   `.pdb` and `.xml` files, and a file present in both is installed once. The build script fails if one of
   `NameShift.exe.config`, `NameShift.Core.dll`, `NameShift.Disk.dll`, `NameShift.Ui.dll` or `NameShift.Cmd.dll` is missing
   from the result. After install, `NameShift.exe` starts and `NameShiftCmd.exe --version` runs (by hand).
9. Given the version, then it is the three-part form (`NameShift-Setup-0.1.0.exe`, the same in `AppVersion` and the Add/Remove
   Programs entry): the first three parts of the built `NameShift.exe`'s file version, passed to `ISCC` as `/DAppVersion=`
   (the `.iss` has a default only for editing). `build-installer.sh` fails if it differs from the `<Version>` in
   `src/NameShift.App/NameShift.App.csproj` (the number `tools/bump-version.sh` sets). Nothing is typed twice.
10. Given the setup window, then its language is English and it shows "NameShift" with the MIT licence page (`LICENSE`). It
    sets the Add/Remove Programs entry (name, version, publisher `Squad-G`, per user), and `/DIR`, `/SILENT` and `/TASKS`
    work on the command line.
11. Given publishing, then David does it by hand: he builds with `installer/build-installer.sh` after `tools/release.sh`, then
    attaches the file to a GitHub release. `installer/README.md` says so in five lines at most, and how to run
    `check-registry.ps1`. The PR tells David that `docs/RELEASING.md` ("Coming later") can be updated by him; the story does
    not edit it.
12. Given the root `README.md`, then its Layout line for `installer/` no longer says "planned, not there yet" and points to
    `installer/README.md`.
13. By hand, written in the PR, on a clean Windows 10 or 11 user: install with all defaults; install with each task
    changed; upgrade over an older build, also while NameShift is open (the window saves its settings at close);
    uninstall with No and with Yes; uninstall while NameShift is open; silent install and uninstall; a `/DIR` of a data
    folder, or of a folder inside one, is refused; `check-registry.ps1` after install and after uninstall; PATH (with
    `%USERPROFILE%` entries) before and after.

## Out of scope
- Code signing, Store, auto-update, a per-machine (all users) install, other languages of setup.
- Changing `tools/release.sh`, `tools/bump-version.sh` or `docs/RELEASING.md`.
- A version stamped on `NameShiftCmd.exe`.
- Verifying how Explorer treats the entry (limits, multi-select, mixed selections, drive roots): NS-49.

## Decisions
- PATH default? **Decision:** off, because it changes the environment and most users never need it (po, 2026-10-18).
- Explorer right-click entry? **Decision (David, 2026-10-18, overriding the po's earlier choice of leaving it out):** the
  installer adds "Bulk Rename Here", on by default, per user; behaviour is NS-49. The compact Windows 11 menu (NS-50) was
  cancelled by David on 2026-10-25: the classic entry is the only one.
- Keys by the installer or by the app? **Decision:** the installer, in `[Registry]` (po, 2026-10-25; NS-49).
- Story order? **Decision (lead, 2026-10-25):** NS-44 first: it creates `installer/` with the three keys and the check
  script; NS-49 then verifies Explorer's behaviour, writes the README section and adjusts the keys if needed. One PR per
  story (po, 2026-10-25).
- Per-user or per-machine? **Decision:** per user only for v1, superseding the earlier "optional per-machine" and "Program
  Files" wording (po, 2026-10-25). Reasons: no admin and no UAC; an elevated install writes `HKCU` of the elevating account,
  which may not be the user's, so the right-click entry and PATH would land in the wrong profile; the program writes only to
  the user's own folders anyway. The lead tells David of this scope change.
- Unsigned? **Decision:** the installer is unsigned for v1: a certificate costs money and needs David's choice; the README
  mentions the SmartScreen warning (po, 2026-10-25).
- Settings and history on uninstall? **Decision:** kept by default; one question with No as default, silent keeps (po,
  2026-10-25): never lose the user's undo history by accident.
- When is the installer built? **Decision (David, 2026-10-24/25):** at the very end, just before the v1 release, so it ships
  the final set of files and the Explorer entry in one go.
- How does it fit David's release? **Decision:** the release scripts stay as they are and know nothing of the installer; the
  installer has its own script and an ignored output folder, so a clean tree is kept for `tools/release.sh` (po, 2026-10-25).
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
- 2026-10-25: re-checked against RELEASING.md, tools/release.sh and the repo; Ready (po).
- 2026-10-25: PR #107 review: order reversed (NS-44 first, no dependency on NS-49), `.gitignore`, pinned three-part version,
  silent-mode wording, PATH kind, data-folder guard, README Layout line, check script execution policy (po).
- 2026-10-25: PR #107 re-review nit folded in: the `/DIR` guard also refuses a folder inside the two data folders
  (criteria 2 and 13) (tech-lead).
- 2026-10-25: In Progress (builder).
