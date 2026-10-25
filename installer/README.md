# Installer

`NameShift.iss` is the Inno Setup 6 script of the per-user setup, `NameShift-Setup-<version>.exe`. It installs into
`%LocalAppData%\Programs\NameShift` with no admin rights, and optionally adds NameShift to PATH and "Bulk Rename Here" to
the Explorer right-click menu. `dotnet build` and `dotnet test` never need Inno Setup.

## Publishing (by hand, owner only)

1. After `tools/release.sh`, run `installer/build-installer.sh` in Git Bash (needs Inno Setup 6, from jrsoftware.org).
2. It writes `installer/Output/NameShift-Setup-<version>.exe` (ignored by Git); attach that file to the GitHub release.
3. After installing, run `powershell -ExecutionPolicy Bypass -File installer\check-registry.ps1 -State Installed`;
   after uninstalling, the same with `-State Removed`. Every line must say PASS.
4. The setup is unsigned, so Windows SmartScreen warns before it runs ("More info", then "Run anyway").
