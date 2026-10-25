# NS-79: Command line: split the apply parser by panel

Status: In Progress
Depends on: NS-40, NS-69
Area: app
Build after NS-40 and NS-69 are merged (NS-69 adds words to the same loop), and before NS-75 and NS-76.

## Why
`CommandLineParser.ParseApply` is about 440 lines after NS-40 and would reach 1,300 with NS-75 and NS-76: every option is a
branch of one `if` chain with its own locals. A chore with no product change: the same command lines give the same results
and the same errors, but a panel's options live in one small class, so the next stories add a class and not a branch. (The NS-40
review asked for it.)

## Acceptance criteria
Code in `src/NameShift.Cmd` only; no change in `Strings.resx`, the help, `NameShift.Ui` or the exit codes.
1. Given the Cmd tests of `dev` at the start, then they pass unchanged on net10.0 and net48 with the same counts, apart from
   the tests added here. Where a message, a precedence (which error wins when several are wrong) or a word has no test of its
   own, the builder first adds a characterization test that passes on the old code (the PR lists them), then does the split.
2. Given the split, then `ParseApply` is a short loop (at most 120 lines) that asks each panel reader in turn whether the
   argument is its own, and the rest of the file is the verbs, `Fail`, `Format` and the help dispatch. One internal reader
   per option group, in `src/NameShift.Cmd`: Replace and Case (stored as NS-69 leaves them, fields of `ApplyOptions` or models), Add, Numbering, the
   output and run switches (`--dry-run`, `--quiet`, `--verbose`) and, after NS-69, the preset options. The tech lead's brief
   picks the exact shape; it must have these properties:
   - a reader owns its words (constants) and its state, reads one argument and the values after it, and says whether the
     word was its own and, if it failed, which usage error;
   - the shared helpers (`TakeOne`, `TakeText`, `TryChoice`, `Is`, `IsOptionWord`, `Fail`, `Format`, the cut of the window's
     text) are one internal class that every reader uses, with the same behaviour as today;
   - the readers build their panel models after the loop (cross-checks such as `--from-end` without `--insert`, and the
     window's error text for numbers) and `ApplyPanels` is built from the readers' models in stage order, with no
     constructor parameter per panel (`Given`, `None` and the properties NS-69 uses stay);
   - no file is over 300 lines.
3. Given the order in which errors are found, then it is unchanged: errors of one argument in argument order (a missing value,
   a repeat, an unknown option, the empty or blank folder, two folders), then after the loop the conflicts (`--quiet` with
   `--verbose`, then `--quiet` with `--dry-run`), then each reader's cross-checks in the order of the code NS-69 leaves: the
   preset options (`--overwrite` without `--save-preset`), then Add (`--from-end` without `--insert`), then Numbering, then the missing folder, then the missing rule. A test with a command line that has
   several mistakes pins the winner for each pair that matters (at least six pairs).
4. Given the words, then `CommandLineParser.Words`, `ApplyOptionWords` and `UndoOptionWords` hold exactly the same words in
   the same order as before, each built from the readers' constants, and a new test asserts that every word of
   `ApplyOptionWords` is claimed by exactly one reader (two readers claiming a word, or a word claimed by none, fails the
   test naming the word). `Words_AreTheDispatchedWords` and NS-43's help tests pass unchanged.
5. Given the extension point, then a test adds a fake reader (internal, in the test project) to the reader list through the
   seam the brief provides and checks that its word is dispatched with no change in the loop: this is the proof that NS-75
   and NS-76 need no edit of `ParseApply`.
6. Given a mutation (one word moved to the wrong reader; the cross-check order swapped), then at least one test fails; the PR
   lists each mutation and the test that failed.

## Out of scope
- New options, help text, error strings (NS-75, NS-76); a command-line library; changing `Parse` for `undo` and `help`.

## Decisions
- Split first (own story) or as the first part of NS-75? **Decision:** its own story, merged before NS-75: a pure refactor is
  reviewed by "same behaviour, same tests", and mixing it with 44 new options would make the review unable to tell a
  moved line from a changed one (po, 2026-10-24).
- Before or after NS-69? **Decision:** after: NS-69 adds `--preset`, `--save-preset` and `--overwrite` to the same loop, and
  two PRs reshaping and extending the same 440 lines would conflict line by line; the split then covers the preset options
  too (po, 2026-10-24).
- Which shape? **Decision:** left to the tech lead's brief, within the properties above (po, 2026-10-24).
- AC3 check order: the story said "Add, Numbering, then the preset options", but the merged NS-69 code checks `--overwrite`
  without `--save-preset` before `--from-end` without `--insert`. **Decision:** keep the code's order (the story's purpose is
  the same errors); AC3 reworded to match and the pair is pinned by a test (lead, 2026-10-24).

## Log
- 2026-10-24: created and set Ready (po), from the NS-40 review note about the size of `ParseApply`.
- 2026-10-24: In Progress (builder).
