# NS-45: Safer Revert: detect changed files

Status: Ready
Depends on: NS-90, NS-15, NS-42
Area: app

## Why
Revert never touches something that was changed after the batch without telling the user first. The check itself is NS-90;
this story is what the window and the command line do with it.

## Acceptance criteria
Logic in `NameShift.Ui` and `NameShift.Cmd` with tests on net10.0 and net48; a new small dialog in `NameShift.App`;
every text in `Strings`. Names are indicative.
1. Given the Revert button and a revertable journal, then the click first runs `CheckRevert()` (NS-90) off the UI thread,
   with the window locked as for a Revert. Given no problem, then Revert runs at once, with no dialog (as today).
2. Given problems, then a dialog lists up to 10 lines `new name: reason` and, if more, `and N more`; the reasons are
   `changed since the rename`, `no longer there`, `its old name is now used by another file` and `waits for another file`.
   The line builder is a `NameShift.Ui` class with tests (the lines, the 10 limit, the singular and plural, the order of
   the journal).
3. Given the dialog, then it has `Revert the rest` (reverts without the changed files), `Revert changed files too`
   (`includeModified: true`), and `Cancel`. `Revert the rest` is shown only when at least one entry is revertable without
   the override; `Revert changed files too` only when at least one entry is `Modified`. When neither applies (every entry
   missing or taken), the dialog has only `Close` and nothing runs. A test table covers the combinations.
4. Given `Cancel` (or Esc, or closing the dialog), then nothing is reverted, the journal is unchanged, Revert stays
   available, and the status bar says `Revert cancelled.`
5. Given a partial revert, then the existing partial-revert sentence of NS-15 reports it and Revert stays available for the
   skipped entries. Given the click when the journal changed between the check and the revert (another window), then
   the executor's own checks still hold (nothing is overwritten).
6. Given `NameShiftCmd undo`, then it never asks: it skips `Modified`, `Missing` and `NameTaken` entries, reverts the rest,
   and reports through the existing result text; a partial revert is exit code 4 as today. There is no option to include
   changed files (the window has the dialog).
7. Given `NameShiftCmd undo --dry-run`, then each entry that would be reverted prints `new -> old` as today, and each entry
   that would be skipped prints `name: skipped (reason)` with the same reason words; exit code 0.
8. Given the window and the command line, then neither renames anything outside the journal's entries.
9. By hand, written in the PR: rename two files, edit one (append text) and delete none: Revert shows the dialog with one
   line; `Revert the rest` restores one name and leaves the edited file under its new name; Revert again shows the same
   dialog; `Revert changed files too` restores it. Then rename, put a new file with an old name in place: the dialog says
   so and the new file is untouched.

## Out of scope
- An `--include-changed` option for the command line; reverting an older batch (one level of undo is the product).
- Hashing content (NS-90).

## Decisions
- Better than classic renamers: classic tools undo blindly. **Decision:** check first, report, never overwrite (po,
  2026-10-18).
- Ask in the window, never in the command line? **Decision:** yes: the window can ask, a script must not wait for an
  answer; the script's safe choice is to skip what changed and say so (po, 2026-10-25).
- Three buttons? **Decision:** the safe default (`Revert the rest`) plus one explicit override for changed files, because a
  user may want the old name back while keeping an edit; the override never overwrites anything (po, 2026-10-25).

## Log
- 2026-10-18: created (po).
- 2026-10-25: checked against dev 57d695b: the journal keeps no size or date, so the check is split into NS-90 (engine);
  this story is the window and the command line; set Ready (po).
