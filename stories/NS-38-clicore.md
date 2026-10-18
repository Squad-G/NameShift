# NS-38: Command line: apply a preset to a folder

Status: Draft
Depends on: NS-36, NS-8, NS-9
Area: app

## Why
Rename from cmd or PowerShell, or from a script, with the same results as the window.

## Acceptance criteria
1. Given `NameShift apply --preset "Photos" "D:\Pics"`, then the saved preset is applied to the files of that folder and they are renamed on disk.
2. Given a file mask instead of a folder (`D:\Pics\*.jpg`), then only matching files are used.
3. Given the same preset and files, then the CLI and the window produce identical new names (both call the same engine and read the same preset format; a test runs both paths on one fixture).
4. Given a preset name that does not exist, then nothing is renamed, a message names the missing preset and lists the available ones.
5. Given the CLI renames files, then it uses the same safe executor and writes the same undo journal as the window (rollback on failure).
6. Given it runs from cmd or PowerShell, then output goes to that console (not a pop-up window) and the shell waits for it to finish.
7. Given no window is needed, then no window is created.

## Out of scope
- Packaging (separate console exe or flags on the main exe) is for the tech lead.

## Decisions
- Who owns mask parsing? **Decision:** the file mask in the path argument (`D:\Pics\*.jpg`) is a shortcut that uses the filter engine's mask (NS-32); NS-41's `--mask` option uses the same code, so there is one mask implementation (po, 2026-10-18).
- Relation to NS-47 (open a folder in the window from the command line)? **Decision:** NS-47 opens the window; this story has no window and always needs a verb (po, 2026-10-18).
- Same engine and preset format? **Decision:** yes, required, so scripts and the window never disagree (po, 2026-10-18).
- Verb or flags? **Decision:** a verb first (`apply`, `undo`), then options, because it is easy to extend and to read in scripts (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
