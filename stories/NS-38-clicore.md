# NS-38: Command line: apply rules to a folder

Status: Done
Depends on: NS-8, NS-9, NS-12, NS-13, NS-73
Area: app

## Why
Rename from cmd or PowerShell, or from a script, with the same results, checks and undo journal as the window.

## Acceptance criteria
Shape (decided by David): a second program, `NameShiftCmd.exe`, a console exe (net48) installed next to `NameShift.exe`: a
thin `Main` over logic in a netstandard2.0 library that takes its arguments, streams and history folder as parameters, so
it is tested on net10.0 and net48. The tech lead picks project names. Every visible string is in a `Strings.resx`.

Syntax: `NameShiftCmd apply <folder> [--replace <find> <with>] [--case lower|upper|title|sentence] [--dry-run]`.
The folder is the one argument that is not an option or an option's value, anywhere on the line (so NS-69's
`apply --preset "Photos" "D:\Pics"` fits). Options may come in any order. At least one rule option is needed. The rules run in the
fixed pipeline order, whatever the typing order. `--replace` matches ignoring case and leaves the extension alone, like the
Replace panel's defaults; `--case` changes the base name only, like the Case panel with no exceptions.

Behaviour:
1. Given a temp folder with `IMG_001.JPG` and `IMG_002.JPG` and `apply <folder> --case lower`, then the files are renamed to
   `img_001.JPG` and `img_002.JPG`, `2 files renamed.` is printed (`1 file renamed.` for one; the strings of `ActionText`)
   and the exit code is 0.
2. Given `apply <folder> --replace "IMG" "Trip"`, then `IMG_001.JPG` becomes `Trip_001.JPG`.
3. Given `--replace x y --case upper` and `--case upper --replace x y`, then the names are identical (fixed order).
4. Given the same folder, files and rules, then the CLI's new names equal the window's: a test runs `PreviewModel` (with
   `ReplacePanel` and `CasePanel` settings equal to the options) and the CLI on one fixture and compares every new name.
5. Given `--dry-run`, then lines `old -> new` (file names only, one per changed file, in list order) are printed, the folder
   and the journal are untouched (names, timestamps, no journal file written) and the exit code is 0. This is the CLI's
   preview.
6. Given the CLI lists the folder, then it uses the window's `DiskFileListSource` and default order (natural name), the
   default `ItemFilter` (NS-32) with `includeFolders: false`: files only; hidden and system files are not renamed (the
   window renames them only when the user ticks "Hidden", NS-70; the CLI has no such option until NS-41's
   `--include-hidden`), but every entry name still counts for the duplicate check.

Shared code, enforced (the CLAUDE.md rule: no rename without the window's checks and journal):
7. The CLI plans with `RenamePlanner.Plan` (NS-73) and renames only with `JournaledRenamer.Rename` on that plan's results and
   conflicts, with `JournaledRenamer.DefaultHistoryRoot` (the window's journal) unless a test gives a temp folder. It never
   renames with `--dry-run`, and never when the plan's `Blocker` is not `None`.
8. A test fails when any source file of the CLI projects contains `RenameExecutor`, `DiskFileSystem`, `File.Move`,
   `Directory.Move`, `.MoveTo(`, `File.Delete` or `Directory.Delete` (a scan of `src/NameShift.Cmd*`, found from
   `NameShift.slnx`; it fails, not skips, when the solution file is not found).
9. The CLI uses the window's wording: `ActionText.ForRename` becomes public (`ActionReport` already is) and the CLI prints
   its status and details; conflicts are printed with `ConflictText.For`; a rule error is printed with the text of the New
   Name cell (`PreviewText`, `Error: ...`), each as `<file name>: <text>`.

Exit codes (complete here; NS-39 adds output modes and `--help`):
10. 0: success, or nothing to rename (`Nothing to rename.` is printed). 1: usage or input error (below). 2: any conflict or rule
    error: nothing renamed, every one printed to the error stream; `--dry-run` reports the same and exits 2.
11. 3: nothing was renamed because the executor refused the batch (`Refused`: a file vanished since the listing, or something
    occupies a target) or rolled it back (`RolledBack`), or because the journal could not be written before the batch
    (`JournalWriteFailure` BeforeStart: the batch never started); the message is `ActionText`'s (it names the file and the
    reason, or says the history could not be saved) and goes to the error stream.
12. 4: the user must look: `NeedsAttention` (items left under other names; each is listed with its current and original path)
    or `JournalWriteFailure` AfterFinish (the names are changed but Revert has no history). Messages from `ActionText`.
13. The CLI logic takes an `IBatchRenamer` (public), with `JournaledBatchRenamer` as the default, so tests can inject
    faults: the internal `HookedFileSystem`, `IFileSystem` and `IJournalFiles` are not visible to the CLI tests. Tests
    give each of 3 and 4 and the three journal outcomes through a fake `IBatchRenamer`, and real-disk tests cover the
    reachable ones (a vanished file, a locked file, an unwritable history folder); each checks the exit code, the stream
    used and that the text is `ActionText`'s.

Input errors (exit 1, nothing renamed, one line on the error stream):
14. A missing folder, a path that is a file, an unreadable folder, an unknown option, a missing value, `--case sideways`
    (the allowed values are listed), no rule option, a repeated `--case` or `--replace`, an empty find text
    (`--replace "" y`), and a folder text that contains a `"` (what cmd produces for `"D:\Pics\"`: the message says to remove
    the trailing backslash).

Console:
15. The exe is a console program, tested: on net48 a test reads the built exe's PE header (Subsystem = 3) and checks that
    neither the exe nor the CLI library nor `NameShift.Ui`, `NameShift.Core` or `NameShift.Disk` reference
    `System.Windows.Forms` (`Assembly.GetReferencedAssemblies`). So output goes to the console, the shell waits, and no
    window is created. Results go to the output stream and errors to the error stream.
16. Given any batch size, then it never prompts (the 500-file confirmation is the window's).
17. Given `NameShift.exe` is started with arguments, then NS-47's behaviour is unchanged.

## Out of scope
- Presets (NS-69), the other rules (NS-40), a mask, recursion, filters and folders (NS-41), `--quiet`, `--verbose` and
  `--help` (NS-39, NS-43; until then a usage error prints a short usage line), `undo` (NS-42).
- Packaging and the installer (NS-44); the exe is only built and tested.
- Running at the same time as the window (no lock across processes, NS-9; to settle in NS-42).
- Handling a trailing backslash inside quotes beyond the clear error of criterion 14.

## Decisions
- Separate console exe or flags on `NameShift.exe`? **Decision:** a separate console exe, because a window-subsystem exe
  returns to the shell at once and cannot write to it reliably, so scripts could not wait for it or read its exit code
  (po, 2026-10-24). **Decided by David (2026-10-24):** a separate console exe named `NameShiftCmd.exe`, installed next to
  `NameShift.exe`.
- Does it need presets? **Decision:** no: the first CLI story takes its rules from options, so it does not wait on presets
  (po, 2026-10-24); preset support is NS-69.
- Preview and undo for the CLI (CLAUDE.md rule)? **Decision:** preview is `--dry-run`, shipped in this story, never
  skipped; undo is the shared journal: a CLI batch is the "last batch", so Revert in the window undoes it now, and `undo`
  from the shell is NS-42. The CLI has no code path that renames without the planner and the journal, enforced by
  criteria 7 and 8 (po, 2026-10-24).
- Exit 4 for a journal failure after a successful run? **Decision:** yes: the names changed but the safety net is missing,
  which a script must not read as plain success (po, 2026-10-24).
- Exit 3 for a refused batch? **Decision:** yes, same as rolled back: in both nothing was renamed because of the disk
  (po, 2026-10-24).
- Who owns mask parsing? **Decision:** the mask is the filter engine's (NS-32); `apply D:\Pics\*.jpg` and `--mask` arrive
  together in NS-41 (po, 2026-10-24).
- Folders renamed by default? **Decision:** no, files only; a script that says `apply` on a folder should not rename its
  sub-folders by surprise (po, 2026-10-24).
- Verb or flags? **Decision:** a verb first (`apply`, later `undo`), then options (po, 2026-10-18).
- After NS-33 (the listing changes)? **Decision:** NS-33 updates this CLI's call of `ListEntries` and `ExistingNames` to
  the new signature, keeping files only and the default filter (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: reshaped to rules-as-options, dry run and conflicts moved in, criteria made testable; set Ready (po).
- 2026-10-24: after review: complete exit codes, shared planner (NS-73) and source-scan guard, public `ActionText`, rule
  errors, PE and WinForms test, input errors; depends on NS-73 (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #60.
- 2026-10-24: Done (tech-lead), PR #60 merged; hand checks for David: run `NameShiftCmd apply "<scratch folder>" --case lower` from cmd and from PowerShell, check the exit code (`%errorlevel%` / `$LASTEXITCODE` is 0), then Revert in the window. For NS-39: exit 4 for an exception before any rename (nothing renamed) could get its own code; the runner's blank-folder guard is unreachable through `Run` (defence in depth).
