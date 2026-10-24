# NS-77: Find and fix the occasional net48 failure in the Ui tests

Status: Ready
Depends on:
Area: app

## Why
`tests/NameShift.Ui.Tests` on .NET Framework 4.8 has misbehaved three times and never twice the same way: one test failed
once, one run executed 1000 of 1011 tests, and one test host hung. None was reproduced. A suite that fails or hangs by
chance trains everyone to rerun red builds, and a hang blocks the merge flow (same reason as NS-58). This is a chore:
no product change.

## Background (what is known)
- Tests run on net10.0 and net48 and in parallel across test classes; the suspects are tests that depend on time, threads
  or a shared folder: `PreviewModelTests` (150 ms debounce, `FakeDelay`), `MainActionsTests` (async actions, about ten
  timing or wait uses), `FolderTreeModelTests`, `FakeFileListSource`, `FakeFolderSource`, `FileListModelTests`,
  `FolderNavigatorTests`, `DiskFileListSourceTests` (real folders under `%TEMP%\NameShift.Ui.Tests`), and the tests that
  scan the repository tree (`AppStyleScanTests`, `PlannerSourceScanTests`, `StyleScanner`), which read files while other
  builds or worktrees write to them.
- The suite is quick (about 1,100 tests, a few seconds), so 50 + 50 loops are realistic.
- On net48 `--blame-hang` needs Sysinternals `procdump` to write a dump, and it is not installed here. The proof is the
  blame sequence file (the last test started) and the `.trx`; a dump is welcome only when `procdump64.exe` is already on
  `PATH` or `PROCDUMP_PATH`, and it is never committed or bundled.
- "1000 of 1011" is a test host that stopped without a result for 11 tests: a crash (a stack overflow or an unhandled
  exception on a background thread kills the host on .NET Framework) or a deliberate abort, not a failed assertion.
- Nothing is changed in `src/` by this story unless the cause is a real race in `NameShift.Ui` (then the fix is in
  `src/` with a test that provokes it, and the story says so).

## Acceptance criteria
1. Given the hunt, then the builder runs the Ui tests on net48 in a loop of at least 50 runs
   (`dotnet test tests/NameShift.Ui.Tests -c Release --no-build -f net48 --blame-hang --blame-hang-timeout 120s
   --blame-crash`, a fresh process per run, the results folder kept per run), and at least 50 more under load (the
   full `dotnet test -c Release --no-build` of the solution at the same time as a second copy of it, or a CPU-burning
   loop on every core), writing down in the PR how many runs failed, hung or crashed. The loop script is not committed
   (a command in the PR is enough).
2. Given a failure, hang or crash is caught, then the PR names the test (from the `.trx` or the blame sequence file),
   the cause (what the test assumed: an order, a delay, a thread, a shared folder, a leaked background task) and shows
   the fix; the blame's sequence file (its last-started test) and the `.trx` are the proof. When a hang leaves no
   sequence file, the builder reruns that loop with `--logger "console;verbosity=detailed"` and the process killed after
   the timeout, and names the last test that printed as started; a dump only if `procdump64.exe` is already available.
3. Given a cause found, then the fix removes the assumption instead of hiding it: no longer retry, no longer sleep
   longer, no `[Fact(Skip=...)]`, no turning off parallelism for the project, and no `[Collection]` that serialises a subset of
   classes as a way around it (unless the tests really share process state, and the PR says why). A test of a
   timing path waits on the event it needs (a `TaskCompletionSource`, the existing `FakeDelay`) and never on the
   wall clock; a test that touches the disk uses its own unique temp folder and removes it in `Dispose`.
4. Given nothing fails in the 100 runs of criterion 1, then the builder widens once (200 runs under load on net48,
   plus a one-core affinity run, `start /affinity 1`, and a run with the thread pool starved), and if it still does not reproduce,
   makes an audit by reading every test that uses `Task`, `await`, `Thread`, `Timer`, `Sleep`, `Delay`, `Wait`,
   `.Result`, `Stopwatch` or the disk: each is either fixed under criterion 3 or listed in the PR as reviewed and
   deterministic with one line of why. The story is then done with a "not reproduced" note rather than left open.
5. Given a test that can leak a background task (a started `Task` never awaited, an `async void`), then the PR lists
   it and fixes it so no task outlives its test (an exception in such a task can kill the net48 host).
6. Given the fix, then the Ui tests on net48 pass 50 runs in a row alone and 20 runs in a row under load, with
   `--blame-hang-timeout` set and no hang reported; the builder pastes the counts in the PR. Net10.0: 20 runs in
   a row pass.
7. Given the guard against a future hang, then `tests/NameShift.Ui.Tests` (and the other test projects, one line each)
   gets a documented way to find a hang quickly: the exact blame command is added to `CLAUDE.md`'s check section by the lead
   in a separate chore PR if David agrees (the builder suggests the text in the PR; this story does not edit `CLAUDE.md`).
8. Given the other test projects (`Core`, `Disk`, `Cmd`), then they are looped 10 times each on both targets in the same
   way; any failure is reported in the PR as a follow-up story (not fixed here unless it is the same cause).
9. Given all of the above, then `dotnet build -c Release` and `dotnet test -c Release --no-build` stay green, the same
   tests exist (no test removed without a replacement), and the PR lists every test file touched.

## Out of scope
- New features or new tests of product behaviour; speeding the suite up; CI setup (there is no CI yet).
- Raising the budget of the NS-58 performance guard.

## Decisions
- Fix, or retry on failure? **Decision:** fix the cause. A retry hides a real race, and the 1000-of-1011 symptom looks like a
  host crash that a retry would only repeat (po, 2026-10-24).
- Not reproduced after the wide hunt? **Decision:** accept an audit with a reviewed list as the result, so the story ends;
  a failure that appears later reopens it with the blame data the loop now makes available (po, 2026-10-24).
- Edit `CLAUDE.md`? **Decision:** not in this story (po cannot, and a builder must not); the builder proposes the
  line and the lead decides (po, 2026-10-24).

## Log
- 2026-10-24: created and set Ready (po): occasional net48 failure, hang and early stop in the Ui tests.
