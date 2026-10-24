# NS-38: Command line: apply rules to a folder

Status: Ready
Depends on: NS-8, NS-9, NS-12, NS-13
Area: app

## Why
Rename from cmd or PowerShell, or from a script, with the same results, checks and undo journal as the window.

## Acceptance criteria
Shape: a second program, `NameShiftCmd.exe`, a console exe (net48) installed next to `NameShift.exe`, thin `Main` over
tested logic that takes its streams and arguments as parameters. The tech lead picks project names and the layout (shared
logic must be testable on net10.0 and net48, and every visible string is in a `Strings.resx`).

Syntax: `NameShiftCmd apply <folder> [--replace <find> <with>] [--case lower|upper|title|sentence] [--dry-run]`.
Options may come in any order. At least one rule option is needed. The rules run in the fixed pipeline order (as in the
window), whatever the typing order. `--replace` matches ignoring case and does not touch the extension, like the Replace
panel's defaults; `--case` changes the base name only, like the Case panel with no exceptions.

1. Given a temp folder with `IMG_001.JPG` and `IMG_002.JPG` and `apply <folder> --case lower`, then the files are renamed on
   disk to `img_001.JPG` and `img_002.JPG`, `N files renamed.` is printed (`1 file renamed.` for one), and the exit code is 0.
2. Given `apply <folder> --replace "IMG" "Trip"`, then `IMG_001.JPG` becomes `Trip_001.JPG`.
3. Given `--replace x y --case upper` and `--case upper --replace x y`, then the names are identical (fixed order).
4. Given the same folder, files and rules, then the CLI's new names equal the window's: a test runs `PreviewModel` (with
   `ReplacePanel` and `CasePanel` settings equal to the options) and the CLI planner on one fixture and compares every new name.
5. Given `--dry-run`, then lines `old -> new` (names only, one per changed file, in list order) are printed, the folder and
   the journal are untouched (checked by file names, timestamps and no journal files written), and the exit code is 0.
   This is the CLI's preview.
6. Given any rename without `--dry-run`, then it goes through `JournaledRenamer` only (same executor, same write-ahead
   journal at `%LOCALAPPDATA%\NameShift\history`, rollback on failure), so the window's Revert undoes a CLI batch
   (verified in a test: CLI batch, then `LoadLastBatch` says it can revert, then Revert restores the names). A batch whose
   journal cannot be written is not started.
7. Given a conflict (duplicate target, existing target, invalid or reserved name, path too long, a rule error), then nothing
   is renamed, each conflict is printed to the error stream with the same text as the window's Status column
   (`ConflictText`), and the exit code is 2. `--dry-run` reports the same and exits 2.
8. Given nothing changes (all names already as the rules would make them), then `Nothing to rename.` is printed and the exit
   code is 0.
9. Given the CLI lists the folder, then it uses the same listing as the window (`DiskFileListSource`): files only for the
   CLI (folders are not renamed; a later story adds the choice), hidden and system files are not renamed, but their names
   still count for the duplicate check; the order is the window's default order (natural name), so numbering and `Replace`
   see the same sequence.
10. Given a missing folder, a path that is a file, an unknown option, a missing value, `--case sideways` or no rule option,
    then nothing is renamed, a one-line message goes to the error stream and the exit code is 1. Unknown or invalid
    `--case` values list the allowed ones.
11. Given a failed batch (a rename fails, rolled back), then the file and the reason are printed to the error stream with
    the window's wording (`ActionText`) and the exit code is 3. (Exit code 4, quiet and verbose modes: NS-39.)
12. Given it runs from cmd or PowerShell, then the program is a console program, so output goes to that console and the
    shell waits for it to finish, and no window is created (it does not reference WinForms). Results go to the output
    stream and errors to the error stream.
13. Given any batch size, then it never prompts: the window's 500-file confirmation does not apply (the preview with
    `--dry-run` is the safety net).
14. Given `NameShift.exe` (the window) is started with arguments, then NS-47's behaviour is unchanged.

## Out of scope
- Presets (NS-69), the other rules (NS-40), a mask, recursion, filters and folders (NS-41), the full exit-code and output
  modes (NS-39), `undo` (NS-42), help and version (NS-43; until then a usage error prints a short usage line).
- Packaging and the installer (NS-44); the exe is only built and tested.
- Running at the same time as the window (no lock across processes, NS-9; to settle in NS-42).

## Decisions
- Separate console exe or flags on `NameShift.exe`? **Decision:** a separate console exe, because a window-subsystem exe
  returns to the shell at once and cannot write to it reliably, so scripts could not wait for it or read its exit code;
  the extra exe is small and shares the same DLLs (po, 2026-10-24). Default; David may override. The exe name
  `NameShiftCmd.exe` is a working name (flagged for David).
- Does it need presets? **Decision:** no: the first CLI story takes its rules from options, so it does not wait on presets
  (NS-36 is on hold); preset support is NS-69 (po, 2026-10-24).
- Preview and undo for the CLI (CLAUDE.md rule)? **Decision:** preview is `--dry-run`, shipped in this story, never
  skipped; undo is the shared journal: a CLI batch is the "last batch", so Revert in the window undoes it now, and
  `undo` from the shell is NS-42. The CLI has no code path that renames without the journal (po, 2026-10-24).
- Who owns mask parsing? **Decision:** the mask is the filter engine's (NS-32); `apply D:\Pics\*.jpg` and `--mask` arrive
  together in NS-41, so there is one mask implementation (po, 2026-10-24).
- Folders renamed by default? **Decision:** no, files only; a script that says `apply` on a folder should not rename its
  sub-folders by surprise (po, 2026-10-24).
- Verb or flags? **Decision:** a verb first (`apply`, later `undo`), then options, because it is easy to extend and to
  read in scripts (po, 2026-10-18).
- Same engine and format? **Decision:** yes, the same engine, listing, checks, executor and journal as the window, so
  scripts and the window never disagree (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-24: reshaped to rules-as-options (no preset dependency), dry run and conflicts moved in, criteria made testable;
  set Ready (po).
