# NS-58: Steady 10,000-file listing test

Status: Done
Depends on: NS-51
Area: app

## Why
`DiskFileListSourceTests.ListEntries_TenThousandFiles_UnderTwoSeconds` asserts a wall-clock time of less than 2 s on
10,000 real files. It failed once on net48 during a full parallel test run (other test classes were hitting the disk
and the CPU at the same time), and a test that fails by chance trains everyone to ignore red builds. The test must
keep catching a slow listing (an accidental quadratic loop) without failing on a busy machine.

## Background (current code)
`tests/NameShift.Ui.Tests/DiskFileListSourceTests.cs` creates 10,000 empty files outside the timed part, then times
`DiskFileListSource.ListEntries` plus `FileListOrder.Sort` once with a `Stopwatch` and asserts `< 2 s`. It runs on
net10.0 and net48 and in parallel with the other test classes of the project. NS-51 criterion 6 ("lists in under 2
seconds, the window stays responsive") is a product promise checked by hand; this test is only its guard.

## Acceptance criteria
1. Given the test, then it keeps its two parts, listing then sorting 10,000 files, and still asserts that all 10,000
   entries came back (the correctness assertion is unchanged).
2. Given the timing, then the listing and sort run once untimed first (warm-up: JIT and the file-system cache), then
   three timed runs of the same two calls; the assertion uses the fastest of the three, so one slow run caused by
   another test cannot fail it.
3. Given the budget, then the assertion is `fastest < 3 s` (a named constant `GenerousBudget` with a comment saying
   it is a regression guard, not the product promise). The fastest of three runs after a warm-up normally takes 15 to
   30 ms; a plain quadratic sort of 10,000 entries takes about 5 s, so it still exceeds the budget.
4. Given a failure, then the message includes all three times, so a real regression and a noisy run can be told apart.
5. Given the test name, then it is renamed `ListEntries_TenThousandFiles_StaysFast` (the old name promises 2 s, which
   it no longer asserts). Nothing else in the other tests of the file changes (the `GenerousBudget` constant of
   criterion 3 is the only addition).
6. Given `dotnet test -c Release` run five times in a row on a development machine, then this test passes all five
   times on net10.0 and net48 (the builder reports the five results in the PR).
7. Given NS-51 criterion 6, then it is unchanged: the 2 s promise stays a hand check.

## Out of scope
- Making other tests serial or turning off parallel test runs (it would slow every run).
- A performance benchmark suite, or a trait that skips the test by default (a skipped test guards nothing).
- Changing `DiskFileListSource` or `FileListOrder`.

## Decisions
- Loosen, skip, or move to a manual run? **Decision:** loosen and steady it (warm-up, best of three, 10 s). A guard
  that runs on every build still catches a quadratic mistake, which is its only job, and it costs a few seconds
  at most. The strict 2 s promise belongs to the hand check in NS-51 (po, 2026-10-23).
- Why 10 s? **Decision:** the listing normally takes well under 1 s, so 10 s is a wide margin for a loaded machine,
  and a 10,000-entry quadratic loop takes far longer (po, 2026-10-23). Superseded by the next line.
- Budget 3 s instead of 10 s? **Decision:** yes. A plain quadratic comparison sort of 10,000 entries measured about
  5 s, so 10 s would not catch it; the fastest of three runs after a warm-up normally takes 15 to 30 ms, so 3 s is
  about 100 times that and still fails the quadratic case (lead, 2026-10-23, from the PR #33 review).

## Log
- 2026-10-23: created and set Ready (po), after one failure on net48 under a full parallel run.
- 2026-10-23: In Progress (builder).
- 2026-10-23: In Review (builder), PR #33.
- 2026-10-24: budget lowered from 10 s to 3 s after the PR #33 review (a quadratic sort takes about 5 s); Done (tech-lead), PR #33 merged into dev.
