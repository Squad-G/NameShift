# NS-65: README layout matches the repository

Status: Ready
Depends on:
Area: docs

## Why
A new contributor reads the "Layout" section of `README.md` to find where things are. It leaves out two source projects
and two test projects, and names an `installer/` folder that does not exist yet.

## Acceptance criteria
Only `README.md` changes (commit and PR title: `[NS-65] README layout`, or `[docs]`). In the "Layout" section, each line is one
project or folder, in this order, with a short plain description:
1. `src/NameShift.Core`: kept as it is (the rename engine; rules, pipeline, conflict checks; no UI).
2. `src/NameShift.Disk`: the only code that renames files on disk: the rename executor and the undo journal.
3. `src/NameShift.Ui`: the window's logic (folder navigation, file list, preview, rule panel models) and every visible text
   (`Strings.resx`). No WinForms reference.
4. `src/NameShift.App`: the WinForms window: thin controls that draw the models of `NameShift.Ui`.
5. `tests/NameShift.Core.Tests`, `tests/NameShift.Disk.Tests` and `tests/NameShift.Ui.Tests`: the xUnit tests of the three
   libraries, run on .NET 10 and on .NET Framework 4.8.
6. `installer/`: marked as planned (`not there yet, see stories/NS-44-installer.md`), because the folder does not exist.
7. `docs/PLAN.md`: kept; and two lines added: `stories/` (the backlog and one file per story) and `docs/RELEASING.md`
   (making a release, owner only).
8. Every path named in the Layout section exists in the repository, except the one marked as planned in 6 (the reviewer
   checks this against the file tree). The other sections of the README (Build, Licence, Branches and releases) are not
   changed, and "NameShift" is the only product name used.
9. The Build section's commands stay as they are, and one line is added after them: the checks every pull request must
   pass (there is no CI; the builder and the reviewer run them): `dotnet build -c Release` then
   `dotnet test -c Release --no-build`.

## Out of scope
- Rewording the introduction, adding screenshots or badges, a contributor guide.
- Changing `CLAUDE.md` or `docs/`.

## Decisions
- Describe the installer folder that is not there? **Decision:** yes, marked as planned, so the README never names a
  folder a reader cannot find and never forgets it once NS-44 lands (po, 2026-10-24).
- Why a story for a docs change? **Decision:** the README is the first page of a public project; keeping it true is as
  reviewable as code (po, 2026-10-24).

## Log
- 2026-10-24: created and set Ready (po).
