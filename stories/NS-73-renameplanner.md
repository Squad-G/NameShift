# NS-73: One planner for the window and the command line

Status: Ready
Depends on: NS-13, NS-52, NS-15
Area: app

## Why
Today the pipeline and the three conflict checks are in the private `PreviewModel.Compute`, and "can Rename run?" is in
`MainActions.CanRename`. The command line (NS-38) must never plan differently from the window, so both call one public
planner. A pure refactor: no change in what the window does.

## Acceptance criteria
In `NameShift.Ui` (namespace `NameShift.Ui.Preview`). Names are indicative.
1. A public static `RenamePlanner.Plan(rules, items, existingNamesByFolder, cancellationToken)` returns a `RenamePlan`: the
   `Results` of `new RenamePipeline(rules).Run(items, token)`, then the `ConflictReport` of `ConflictChecker.Run` with
   `InvalidNameCheck`, `DuplicateTargetCheck` (given the map of every entry name per folder) and `PathLengthCheck`, in this
   order. It throws `OperationCanceledException` when the token is cancelled, as `Compute` does today. The folder map
   today holds the one listed folder; its type is `IReadOnlyDictionary<string, IReadOnlyCollection<string>>` so sub-folders
   (NS-33) need no change here.
2. `RenamePlan` exposes `Results`, `Conflicts`, `ChangedCount`, `ErrorCount`, `ConflictCount`, `HasConflicts` and one
   property `Blocker` (`None`, `NothingToRename`, `Conflicts`, `RuleErrors`, `ConflictsAndRuleErrors`): `NothingToRename` when
   no result has status Changed and there is no conflict and no error; the others as in NS-15:2. The rule "Rename needs a
   change, no conflict and no rule error" is written there and nowhere else.
3. `PreviewModel` calls `RenamePlanner.Plan` (its private `Compute` keeps only the snapshot wrapping), `PreviewSnapshot`
   is built from the plan, and `MainActions.CanRename` and `ActionText.Hint` read `Blocker` instead of their own tests.
   `ActionText.Hint` still returns "" for `NothingToRename` (as today when there is no conflict or error). The public
   `PreviewSnapshot` constructors stay, with `Blocker` derived from their arguments, so the existing tests compile and
   pass unchanged. Every existing test of the preview, the actions and the hints passes unchanged.
4. Tests of the planner: a fixture with a changed name, an unchanged name, a rule error, an invalid character, a duplicate
   target, an existing target and a path that is too long gives the expected `Blocker` for each combination and the same
   conflict codes as running the pipeline and the three checks by hand; a cancelled token throws; an empty list gives
   `NothingToRename`.
5. The three checks are named in one place only (the planner): a test fails if `PreviewModel.cs` still contains
   `InvalidNameCheck` (a source scan, found from `NameShift.slnx`; the test fails when the file is not found).

## Out of scope
- Any change in behaviour, in the executor or in the CLI (NS-38).

## Decisions
- Why a story of its own? **Decision:** a shared planner is the only thing that makes "the CLI plans like the window" true
  by construction instead of by a comparison test; it is a small refactor that is easier to review alone (po, 2026-10-24,
  from the PR #53 review).

## Log
- 2026-10-24: created (po); Ready, to be built before NS-38.
