# NameShift

A free, open-source, lightweight bulk file renamer for Windows.

Pick a folder, stack up rename rules (regex, replace, case, remove, add, numbering, dates…), see every new name
live before anything is touched, then rename in one click, and undo it if needed.

- **Tiny:** a native WinForms app on .NET Framework 4.8, which ships with Windows 10 and 11. No runtime to download;
  the installer is a few MB at most.
- **Safe:** live preview, conflict detection before renaming, one-click undo of the last batch.

## Build

Requires the .NET 10 SDK on Windows (the app itself targets .NET Framework 4.8, built into Windows).

```
dotnet build
dotnet test
```

Every pull request must pass these checks (there is no CI; the builder and the reviewer run them):

```
dotnet build -c Release
dotnet test -c Release --no-build
```

### Reading a test run

Run the tests so that a hang is named, and keep the whole output in a file outside the repository:

```
dotnet test -c Release --no-build --blame-hang-timeout 5m > <output file> 2>&1
```

- The run is green only when the exit code is 0 **and** each of the eight `Passed!` lines (four test projects, two
  frameworks each) shows the full total. Compare the totals with the previous run, not just the `Failed:` column.
- A hung test host is killed after the timeout and still prints `Passed!` with a **short total** and `Failed: 0`;
  `Test Run Aborted` and `The test running when the crash occurred:` (the hung test) follow that line. The tests of
  that class still queued after it are missing from the total.
- A failed test is printed as `Failed <test name>` with its message and stack trace, before the summary lines.
- To find a hang or a crash in one project, run it alone with the blame sequence file, for example
  `dotnet test tests/NameShift.Ui.Tests -c Release --no-build -f net48 --blame-hang --blame-hang-timeout 120s --blame-crash --results-directory <folder>`:
  the last test in `<folder>/<guid>/Sequence_*.xml` is the one that never finished. Add
  `--logger "trx;LogFileName=run.trx"` to get every result by name.

The app is `src/NameShift.App/bin/<Configuration>/net48/NameShift.exe`.
The command line is `src/NameShift.Cmd.Exe/bin/<Configuration>/net48/NameShiftCmd.exe`.

## Layout

- `src/NameShift.Core`: the rename engine (rules, pipeline, conflict checks). No UI, fully unit-tested.
- `src/NameShift.Disk`: the only code that renames files on disk: the rename executor and the undo journal.
- `src/NameShift.Ui`: the window's logic (folder navigation, file list, preview, rule panel models) and every visible
  text (`Strings.resx`). No WinForms reference.
- `src/NameShift.App`: the WinForms window: thin controls that draw the models of `NameShift.Ui`.
- `src/NameShift.Cmd`: the command line's logic and its texts (`Strings.resx`): it plans with the window's planner and
  renames only through the undo journal. No WinForms reference.
- `src/NameShift.Cmd.Exe`: `NameShiftCmd.exe`, the console program; its `Main` only calls `NameShift.Cmd`.
- `tests/NameShift.Core.Tests`, `tests/NameShift.Disk.Tests`, `tests/NameShift.Ui.Tests` and
  `tests/NameShift.Cmd.Tests`: the xUnit tests of the four libraries, run on .NET 10 and on .NET Framework 4.8 (the
  tests of `NameShiftCmd.exe` itself on .NET Framework 4.8 only).
- `installer/`: the per-user Inno Setup installer, its build script and the registry check; see
  [installer/README.md](installer/README.md).
- `docs/PLAN.md`: features and build order.
- `stories/`: the backlog and one file per story.
- `docs/RELEASING.md`: making a release (owner only).

## Licence

MIT. NameShift is an independent project, not affiliated with any other renaming tool.

## Branches and releases

- `dev`: where work lands, through pull requests. Contributions welcome.
- `prod`: the latest release. Only the owner can push it.
- `releases/vX.Y.Z`: one locked branch per release, kept forever. Only the owner creates them; nobody can change or
  delete them.

Making a release (owner only): see [docs/RELEASING.md](docs/RELEASING.md).
