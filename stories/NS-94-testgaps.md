# NS-94: Close two gaps in the tests: PathInput prefixes and both orders of the filter binding

Status: Ready
Depends on: NS-10, NS-70, NS-78
Area: app
Chore: tests only; no change to `src`.

## Why
Two review follow-ups: a branch of `PathInput.TryExact` that no test reaches, and a rule of `FilterBinding` (either order of
two runs ends the same) that the tests pin for one order only.

## Acceptance criteria
Tests in `tests/NameShift.Ui.Tests` on net10.0 and net48. No file under `src` is edited (if a test shows a bug, it is
written up in the PR and a story is made, not fixed here).
1. Given `PathInput.TryExact`, then rows: `\\.\pipe\x` and `\\.\C:\x` give `Invalid`; `\\?\C:\x` and `\\?\UNC\server\share\x`
   give `Invalid`; `\\server\share\x` and `C:\x` give `Ok` (the prefixes are the only difference); a path that is `Ok` comes
   back unchanged (no trim, no quote stripping).
2. Given the first row set, then a mutation check in the PR: with the `\\.\` condition removed from `TryExact`, the row
   `\\.\pipe\x` fails (it passes `IsFullyQualified` as a server called `.`). The `\\?\` condition cannot be reached on its own,
   because the `?` is caught first as a forbidden character; the test pins the result and the PR notes that the condition is
   defence in depth.
3. Given `FilterBinding` (NS-70, NS-78: the filter run and the Sub-folders reload for one change), then there are two tests
   with the same change (a Reset that changes the box and a filter value, as in the existing `ChangeHeld` test): in one the
   reload lands first, in the other the filter run lands first (its work ends before the handler returns, the order the
   existing helper holds back). Both end with the same list: the unfiltered folder-only listing, and the same
   `SetFilterAsync` and `SetIncludeSubfoldersAsync` calls.
4. Given the new tests, then they are not flaky: 200 runs in a loop on both runtimes pass (written in the PR), and they use
   the existing gates (`TestGate`, `TestWait`), never a sleep.
5. Given a mutation (the binding asking for the reload before the filter), then at least one of the two tests fails; the PR
   says which.

## Out of scope
- Changing `PathInput` or `FilterBinding`.

## Decisions
- Test the unreachable `\\?\` branch separately? **Decision:** no: a test that cannot fail for that branch would only look
  like coverage; the row pins the outcome (po, 2026-10-25).

## Log
- 2026-10-25: created from the NS-10, NS-70 and NS-78 review follow-ups; set Ready (po).
