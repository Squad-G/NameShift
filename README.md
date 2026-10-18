# NameShift

A free, open-source, lightweight bulk file renamer for Windows.

Pick a folder, stack up rename rules (regex, replace, case, remove, add, numbering, dates…), see every new name
live before anything is touched, then rename in one click, and undo it if needed.

- **Tiny:** a native WinForms app on .NET Framework 4.8, which ships with Windows 10 and 11. No runtime to download;
  the installer is a few MB at most.
- **Safe:** live preview, conflict detection before renaming, one-click undo of the last batch.

## Build

Requires the .NET SDK (8 or later) on Windows.

```
dotnet build
dotnet test
```

The app is `src/NameShift.App/bin/<Configuration>/net48/NameShift.exe`.

## Layout

- `src/NameShift.Core`: the rename engine (rules, pipeline, conflict checks). No UI, fully unit-tested.
- `src/NameShift.App`: the WinForms window.
- `tests/NameShift.Core.Tests`: xUnit tests for the engine.
- `installer/`: the Inno Setup script.
- `docs/PLAN.md`: features and build order.

## Licence

MIT. NameShift is an independent project, not affiliated with any other renaming tool.

## Branches and releases

- `dev`: where work lands, through pull requests. Contributions welcome.
- `prod`: the latest release. Only the owner can push it.
- `releases/vX.Y.Z`: one locked branch per release, kept forever. Only the owner creates them; nobody can change or
  delete them.

Making a release (owner only): see [docs/RELEASING.md](docs/RELEASING.md).
