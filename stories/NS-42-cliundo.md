# NS-42: Command line: undo the last batch

Status: Draft
Depends on: NS-38, NS-9, NS-43
Area: app

## Why
Undo from the shell, with the same journal and the same rules as the window's Revert: a script (or a person who ran
`apply` from the prompt) can take the last batch back without opening the window. Draft only until NS-43 is merged (the
verb dispatch and the help test it adds); the criteria are final. It does not wait for NS-45: skipping and listing what
cannot be restored is already NS-9:8, and NS-45 will improve both the window and `undo` through the same `Revert()`.

## Acceptance criteria
Code: a verb in `src/NameShift.Cmd` (`CommandLineParser`, `CommandRunner`), no change in `NameShift.Disk`. It reverts only
through `IBatchRenamer.Revert()` (the window's `JournaledBatchRenamer`) and reads the journal only through
`LoadLastBatch()`, which it calls first because `Revert()` alone answers `NothingToRevert` for all four states that
cannot be reverted (criteria 3 and 4 need to tell them apart). The source scan of NS-38:8 still passes unchanged.
1. Given `NameShiftCmd undo`, then the last batch (made by the CLI or by the window, the journal is the same file) is
   reverted through `IBatchRenamer.Revert()` and the result is written with `ActionText.ForRevert` (the window's words:
   `3 names restored.`, `1 name restored.`; the details list is written after the status line).
2. Given a CLI batch of `IMG_001.JPG`, `IMG_002.JPG` renamed with `--case lower` in a temp folder (temp history root),
   then `undo` gives the names `IMG_001.JPG` and `IMG_002.JPG` back and exits 0; a second `undo` prints the window's
   `There is nothing to revert.` and exits 0 (one level of undo, NS-9:2). A test also reverts a window-made batch
   (a `JournaledBatchRenamer.Rename` on the same root) with `undo`.
3. Given no batch was ever made, or the last one was already reverted (`LastBatchState` None or Reverted), then
   `There is nothing to revert.` goes to the output stream and the exit code is 0.
4. Given the journal is corrupt or a batch or revert was cut short (`LastBatchState` Unreadable or Interrupted), then
   nothing is changed, the journal file is not touched, and the error stream gets a new string: `The undo history cannot
   be used: a rename was cut short or the history file is unreadable. No file was changed.`; exit 3. A test for each
   state (an invalid `last-batch.json`; a stale `pending.json`).
5. Given some files cannot be restored (removed, or the old name is taken: `RevertOutcome` PartlyReverted or
   NothingReverted), then nothing is overwritten and the window's list (`ActionText.ForRevert` details, capped at 20 lines
   as in the window) goes to the error stream after the status line, and the exit code is 4 for PartlyReverted (the
   user must look at the remaining files) and 3 for NothingReverted (nothing changed). The entry stays revertable as
   NS-9:8 says; a test checks that a following `undo` retries the skipped files once the cause is fixed.
6. Given `RevertOutcome.NotStarted` (the journal could not be written before the revert) the exit code is 3, with the
   window's `ForRevert` text on the error stream. Given `NeedsAttention`, or a `JournalIssue` of AfterFinish, the exit
   code is 4 with the window's text on the error stream. Given an exception from `Revert()`, the exit code is 4 with
   `Strings.ActionUnexpected` (the disk state is unknown), as for `apply` (NS-43:8). Each outcome is tested through a
   fake `IBatchRenamer` and the stream and the text are checked against `ActionText`.
7. Given `NameShiftCmd undo --dry-run`, then `LoadLastBatch()` is read and, for each entry of the batch, one line
   `<current file name> -> <name it would get back>` (file names, journal order) is written to the output stream, then
   exit 0; no file, no journal and no `pending.json` is touched or written (a test compares names, timestamps and the
   history folder before and after). The preview reads the journal only: it does not check the disk, so it cannot
   promise every file will be restorable; the help line for `--dry-run` says so. With no revertable batch it behaves
   as criteria 3 and 4, with the same exit codes.
8. Given `undo` with a folder, a rule option or any other argument but `--dry-run`, `--help`, `-h` or `/?`, then a usage
   error (exit 1, one line on the error stream as in NS-43:4) and nothing is reverted. `undo` has no folder: the journal
   knows its folders.
9. Given `undo --help` (NS-43), then its usage, the `--dry-run` option and an example are written, exit 0; the main help
   lists the verb (NS-43's help test fails otherwise). The exit codes of `undo` extend NS-43's `Strings.ExitCode3` and
   `Strings.ExitCode4` wording (the history cannot be used, or no file could be restored, for 3; some files not restored,
   for 4), and NS-43:8's test reads the extended text. An exception from `LoadLastBatch()` (before `Revert()` is
   called) is exit 3 with `Strings.UnexpectedBeforeRename`-style text for undo ("No file was changed."), as NS-43:9.
10. Given any size of batch, then `undo` never prompts and never reads input (scripts never block). The window's Revert
    needs no confirmation either (NS-15).
11. Given `undo` runs while no folder or file is given, then it works from any current directory (a test starts it in a
    temp folder unrelated to the batch).

## Out of scope
- Undoing more than the last batch (not planned, NS-9), choosing a batch by id, `--quiet`/`--verbose` for `undo` (NS-39 may
  extend it later).
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
  window's words so both match what the journal calls it (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: rewritten after NS-38 merged: depends on NS-9 and NS-43 instead of NS-45, outcomes mapped to the exit
  table, dry run, no lock; Draft until NS-43 is merged (po).
