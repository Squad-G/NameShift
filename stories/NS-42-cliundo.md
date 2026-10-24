# NS-42: Command line: undo the last batch

Status: Done
Depends on: NS-38, NS-9, NS-43
Area: app

## Why
Undo from the shell, with the same journal and the same rules as the window's Revert: a script (or a person who ran
`apply` from the prompt) can take the last batch back without opening the window. It does not wait for NS-45: skipping
and listing what cannot be restored is already NS-9:8, and NS-45 will improve both the window and `undo` through the
same `Revert()`. First of the four CLI follow-ups (build order: NS-42, NS-39, NS-40, NS-69); it also makes the usage
lines generic so the later stories never edit them again (criterion 12).

## Acceptance criteria
Code: a verb in `src/NameShift.Cmd` (`CommandLineParser`, `CommandKind`, `ParsedCommand`, `CommandRunner`, `ExitCodes`,
`HelpText`, `Strings.resx`), no change in `NameShift.Disk`. It reverts only through `IBatchRenamer.Revert()` (the
window's `JournaledBatchRenamer`) and reads the journal only through `LoadLastBatch()`, which it calls first because
`Revert()` alone answers `NothingToRevert` for all four states that cannot be reverted (criteria 3 and 4 need to tell
them apart). `CmdSourceScanTests` passes unchanged (no `Delete(` or `Move(` in the Cmd sources).
1. Given `NameShiftCmd undo`, then the last batch (made by the CLI or by the window, the journal is the same file) is
   reverted through `IBatchRenamer.Revert()` and the result is written with `ActionText.ForRevert` (the window's words:
   `3 names restored.`, `1 name restored.`; the details list is written after the status line).
2. Given a CLI batch of `IMG_001.JPG`, `IMG_002.JPG` renamed with `--case lower` in a temp folder (temp history root),
   then `undo` gives the names `IMG_001.JPG` and `IMG_002.JPG` back and exits 0; a second `undo` prints the window's
   `There is nothing to revert.` and exits 0 (one level of undo, NS-9:2). A test also reverts a window-made batch
   (a `JournaledBatchRenamer.Rename` on the same root) with `undo`.
3. Given no batch was ever made, or the last one was already reverted (`LastBatchState` None or Reverted), then
   `Ui.Strings.NothingToRevert` (`There is nothing to revert.`) goes to the output stream, exit 0, and `Revert()` is
   not called (the fake counts its calls).
4. Given the journal is corrupt or a batch or revert was cut short (`LastBatchState` Unreadable or Interrupted), then
   nothing is changed, `Revert()` is not called, the journal file is not touched, and the error stream gets the new
   string `Strings.UndoHistoryUnusable`: `The undo history cannot be used: a rename was cut short or the history file is
   unreadable. No file was changed.`; exit 3. A test for each state (an invalid `last-batch.json`; a stale
   `pending.json`).
5. Given some files cannot be restored (removed, or the old name is taken: `RevertOutcome` PartlyReverted or
   NothingReverted), then nothing is overwritten and the window's list (`ActionText.ForRevert` details, capped at
   `ActionText.DetailLines` = 20 lines as in the window) goes to the error stream after the status line, and the exit
   code is 4 for PartlyReverted (the user must look at the remaining files) and 3 for NothingReverted (nothing
   changed). The entry stays revertable as NS-9:8 says; a test checks that a following `undo` retries the skipped
   files once the cause is fixed.
6. Given `RevertOutcome.NotStarted` (the journal could not be written before the revert) the exit code is 3, with the
   window's `ForRevert` text on the error stream. Given `NeedsAttention`, or `Reverted` with a `JournalIssue` of
   AfterFinish, the exit code is 4 with the window's text on the error stream. The mapping is
   `ExitCodes.For(RevertResult)`, next to the existing `For(RenameRunResult)`: NothingToRevert 0; Reverted 0 (4 with an
   AfterFinish issue); PartlyReverted 4; NothingReverted 3; NotStarted 3; NeedsAttention 4; one test per row. Given an
   exception from `Revert()`, the exit code is 4 with `Ui.Strings.ActionUnexpected` (the disk state is unknown), as for
   `apply` (NS-43:9): the runner's "renamer was called" flag is set just before `Revert()` is called, so the catch-all
   in `Run` already picks exit 4. Each outcome is tested through a fake `IBatchRenamer` and the stream and the text are
   checked against `ActionText`. Output goes to the output stream on exit 0 and to the error stream otherwise, as in
   `apply`.
7. Given `NameShiftCmd undo --dry-run`, then `LoadLastBatch()` is read and, for each entry of the batch, one line
   `<current file name> -> <name it would get back>` (`Path.GetFileName` of `NewPath`, then of `OldPath`; journal
   order; written with `Strings.DryRunLine`) is written to the output stream, then exit 0; no file, no journal and no
   `pending.json` is touched or written, and `Revert()` is not called (a test compares names, timestamps and the history
   folder before and after). The preview reads the journal only: it does not check the disk, so it cannot promise every
   file will be restorable; the help line for `--dry-run` says so (criterion 9). With no revertable batch it behaves as
   criteria 3 and 4, with the same exit codes.
8. Given `undo` followed by anything but `--dry-run` (a folder, `--case lower`, `--replace a b`, any other word), then a
   usage error (exit 1, one line on the error stream in the NS-43:4 shape: message, `Strings.Usage`,
   `Strings.HelpPointer`) with the new message `Strings.UndoOnlyDryRun`: `undo takes no folder or rule options, only
   --dry-run.`, and nothing is reverted. A repeated `--dry-run` is the usual `Strings.RepeatedOption` error. `undo` has no
   folder: the journal knows its folders. `-h`, `--help` and `/?` anywhere after `undo` win over errors, as after
   `apply` (NS-43 decision), and print the help of criterion 9.
9. Given `undo --help` (NS-43), then its help is written (exit 0, nothing read or changed): `HelpUsageUndo`, then
   `HelpUndo`, which has exactly these lines (each at most 79 ASCII columns):
   ```
   Options of undo:
     --dry-run       List the names undo would restore; change nothing. Reads the
                     history only: it does not check the files.
     --help, -h, /?  Show this help.
   Undo takes back the last batch, also one made in the window.

   Example of undo:
     NameShiftCmd undo --dry-run
   ```
   The main help lists the verb in `HelpMain` (`  undo   Take back the last batch (also one made in the window).` in the
   Commands list, and the example `  NameShiftCmd undo --dry-run` in the Examples), shows both usage lines under one
   `Usage:` header, and includes `HelpUndo` after `HelpApply`. NS-43's tests pass with the new words:
   `CommandLineParser.Words` and `GlobalWords` gain `undo`; a new `UndoOptionWords` (`--dry-run`) is checked against the
   tokens of `HelpUndo` as `ApplyOptionWords` is against `HelpApply`; `HelpLines_AtMost79_AsciiOnly` and
   `HelpTexts_AreInTheComposedHelps` cover `HelpUsageUndo` and `HelpUndo` too (`HelpText.Undo()` returns the help of
   `undo`, `CommandKind.UndoHelp`). `CommandLineParserTests.Words_AreTheDispatchedWords` (which pins the exact list) is
   updated to the new list, and its `Words == GlobalWords.Concat(ApplyOptionWords)` check stays true (`UndoOptionWords`
   repeats `--dry-run` but is not part of `Words`).
10. Given any size of batch, then `undo` never prompts and never reads input (scripts never block). The window's Revert
    needs no confirmation either (NS-15).
11. Given `undo` runs while no folder or file is given, then it works from any current directory (a test starts it in a
    temp folder unrelated to the batch).
12. Given the texts that every later CLI story would otherwise edit, then this story rewrites them once:
    - `Strings.Usage` becomes `Usage: NameShiftCmd apply <folder> <options> | NameShiftCmd undo [--dry-run]` (still the
      one line of every usage error; `CmdStringsTests.Usage_NamesTheExeAndEveryOption` is renamed and compares this text).
    - `Strings.NoCommand` becomes `Give a command: apply or undo.`
    - `HelpUsageApply` becomes `Usage:` and, on the next line, `  NameShiftCmd apply <folder> <options>` (no
      `--dry-run` in it, so `ApplyHelp_HasItsUsageOptionsAndOneExample` still finds one example line only);
      `HelpUsageUndo` is `Usage:` and `  NameShiftCmd undo [--dry-run]`.
    - The `apply` line of `HelpMain` becomes `  apply   Rename the files of a folder with rule options.` (aligned with the
      other lines).
    - `Strings.ExitCode3` becomes `Nothing changed (refused, rolled back, no history, or a crash before it).` and
      `Strings.ExitCode4` becomes `Look at the disk: items left renamed or not restored, no history, a crash.`
      (the help prints `  <number>  <text>`: both stay at most 79 columns; if a tweak is needed to fit, keep the
      meaning), and the `ExitCode` summaries in `ExitCode.cs` get the undo meaning (3: nothing changed; 4: some files
      not restored). `EveryExitCode_HasItsLine` reads the new texts by key, so it passes unchanged.
    - New strings: `UndoHistoryUnusable`, `UndoOnlyDryRun`, `HelpUsageUndo`, `HelpUndo` (criteria 4, 8, 9) and
      `UnexpectedBeforeUndo`: `NameShift hit an unexpected problem. No file was changed.` for an exception raised before
      `Revert()` is called (`LoadLastBatch()` never throws by contract, but a fake or a listing bug can): exit 3, as NS-43:9
      does for `apply`. `CmdStringsTests.EveryKeyResolves` passes.
13. Given the merged tests that assume the CLI never touches the journal (`FakeBatchRenamer.LoadLastBatch` and `Revert`
    throw `NotSupportedException`), then they stay for `apply`: the fake gets settable results and call counters that
    default to the throwing behaviour, so an `apply` that reads or reverts the journal still fails its test.

## Out of scope
- Undoing more than the last batch (not planned, NS-9), choosing a batch by id, `--quiet`/`--verbose` for `undo` (NS-39
  does not extend it; revisit if asked).
- A lock between the window and the shell (see Decisions), refreshing an open window after a shell `undo`.
- Detecting files modified since the batch (NS-45): when it lands, `undo` gets it through `Revert()` and its criteria
  are added to this story's tests then.

## Decisions
- Depend on NS-45? **Decision:** no. The partial revert already never overwrites and lists what it skipped (NS-9:8), which is
  what a script needs; NS-45 adds a pre-check and a window dialog, and `undo` will inherit the pre-check without a
  design change. The first version of this story waited for it, which would have held the undo story behind a window
  dialog (po, 2026-10-24).
- Exit codes for undo? **Decision:** the same five as `apply`, same meaning: 0 done or nothing to undo; 1 usage; 3 nothing
  was restored (the history cannot be used, the revert could not start, or no file could be restored); 4 something is
  left for the user (some files not restored, items stranded, history not saved after the revert, an unexpected error).
  A script then reads `apply` and `undo` with one table (po, 2026-10-24).
- Unreadable or interrupted history: "nothing to undo"? **Decision:** no, exit 3 with its own message. Saying "nothing
  to undo" about a history that is in fact broken would let a script (or a person) believe the batch is gone for good;
  the window just disables the button, but a command line has to say why (po, 2026-10-24).
- `undo --dry-run` shows the journal, not a disk check? **Decision:** yes: the real checks live in the disk planner,
  which is internal to `NameShift.Disk`; making it public for a preview would widen the Disk API for little gain, and the
  line says exactly what the journal holds. If a file is missing the real `undo` reports it (po, 2026-10-24).
- Window and shell at the same time (the question left open in NS-9)? **Decision:** no cross-process lock. The journal
  writes are atomic and `pending.json` marks an operation in flight, so a crash or an overlap reads as `Interrupted`
  (Revert disabled) rather than as a wrong undo; two renames of the same journal within the same few milliseconds are
  not a case worth a named mutex and a wait policy. The window re-reads the journal at every Revert, so a shell `undo`
  makes the window's next Revert say `There is nothing to revert.` (a test with two `JournaledBatchRenamer`
  instances on one root). Revisit if a real report shows an overlap (po, 2026-10-24).
- Verb name? **Decision:** `undo` (the window's button is Revert, but a shell user says undo); the output uses the
  window's words so both match what the journal calls it. Some of the window's sentences say "Revert stays available";
  they are printed as they are, so one vocabulary serves the window and the shell (po, 2026-10-24).
- Undo's exception string? **Decision:** `NameShift hit an unexpected problem. No file was changed.` (key
  `UnexpectedBeforeUndo`): `UnexpectedBeforeRename` says "renamed", which is wrong for a restore, and "changed" is true
  of both verbs (po, 2026-10-24).
- Generic usage lines? **Decision:** `Strings.Usage` and the help usage lines name the verbs and `<options>`, not every
  option: the options are in `apply --help`, and four more stories add options, each of which would otherwise rewrite
  the one-line usage that ends every error and its exact-text test (po, 2026-10-24).
- Exit 3 and 4 wording for both verbs? **Decision:** one line each, covering `apply` and `undo`; the one-line limit of the
  help table (79 columns with the indent) forces short wording, so the detail stays in the story and in `ExitCode.cs`
  (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: rewritten after NS-38 merged: depends on NS-9 and NS-43 instead of NS-45, outcomes mapped to the exit
  table, dry run, no lock (po).
- 2026-10-24: NS-43 merged. Re-checked against its code: generic usage lines, undo help and exit wording written out,
  the exception string chosen, journal-first call order and the fake's defaults added; set Ready, first of the CLI
  build order NS-42, NS-39, NS-40, NS-69 (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #71.
- 2026-10-24: Done (tech-lead), PR #71 approved and merged (review nits 3 and 4 applied by the builder). Hand checks
  pending for David: `--help` layout in cmd, PowerShell and Windows Terminal; apply, `undo --dry-run`, `undo`, `undo`
  round trip on a scratch folder; the window's Revert finds nothing after a shell undo.
