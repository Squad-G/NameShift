# NS-94: Close gaps in the tests: PathInput prefixes, both orders of the list model, broken path literals

Status: Done
Depends on: NS-10, NS-70, NS-78, NS-80
Area: app
Chore: tests only; no change to `src`.

## Why
Review follow-ups: a branch of `PathInput.TryExact` that no test reaches, a rule `FilterBinding` relies on (either order of
filter and reload ends the same) that the tests pin for one order only, and a guard against path literals broken by editors.

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
3. Given `FileListModel` (the object `FilterBinding` drives), then two tests await the two calls one after the other, in
   each order, on the same loaded folder: `SetFilterAsync(filter)` then `SetIncludeSubfoldersAsync(true)`, and the reverse,
   each followed by the matching un-do (a Reset to the default filter and box off). In every case the list ends equal to
   the list of the final settings (names and order), the filter and the box read back what was set, and the source was
   asked for sub-folders exactly as the box says. This pins "either order ends the same", the rule `FilterBinding` relies
   on, with no timing involved: the existing `ChangeHeld` tests keep pinning the binding's own order. (The case "the filter
   run lands before the handler returns" cannot be pinned without a seam in `src`, and it is the race NS-77 removed; it is
   deliberately not tested.)
4. Given the new tests, then they use no sleep and no race (every call is awaited), and 200 runs in a loop on both runtimes
   pass (written in the PR).
5. Given a mutation (the reload ignoring the filter set last), then at least one of the two tests fails; the PR says which.
6. Given a guard against broken backslash escapes (agents' editors keep turning `\n` and `\a` inside a path literal into a
   line break or a control character: NS-80 found `\b` and `\a` as control bytes; the other form is `@"C:` at the end of a
   line and `owhere"` at the start of the next, from `@"C:\nowhere"`), then a pure function in the test project (text in,
   `line` and a short reason out, like NS-80's scan, using its shared root finder) flags, in every `.cs` file under `src`
   and `tests`, a verbatim string that is opened with `@"` (or `$@"`) and not closed on the same line when the text after
   the quote reads like the start of a path: a drive letter and a colon, optionally a backslash and path characters
   without a space, up to the end of the line (`@"C:`, `@"C:\Tem`). Quotes doubled as `""` do not close it. A verbatim
   string that spans lines on purpose (SQL-like text, a multi-line sample) is not flagged because it does not start like
   a path. Tests for the function: the broken `@"C:` and `@"C:\Tem` lines, a closed `@"C:\x"`, a closed `@"C:\x"" y"`,
   a multi-line text that starts with `@"Line one`, `$@"D:` flagged, a comment line holding `@"C:` flagged (accepted: no
   comment in the repository needs it). Run over the whole tree it finds nothing, and it finishes in under two seconds. The
   PR names the mutation that makes a test fail (the function not counting `""`).

## Out of scope
- Changing `PathInput` or `FilterBinding`; other kinds of broken escapes (a tab or control byte is NS-80's scan; a missing
  escape that still compiles cannot be found by a scan).

## Decisions
- Test the unreachable `\\?\` branch separately? **Decision:** no: a test that cannot fail for that branch would only look
  like coverage; the row pins the outcome (po, 2026-10-25).

## Log
- 2026-10-25: PR #90 review: the second-order test is made deterministic at `FileListModel` (no seam); the broken-escape
  guard added (po).
- 2026-10-25: created from the NS-10, NS-70 and NS-78 review follow-ups; set Ready (po).
- 2026-10-25: built: TryExact prefix rows, ListOrderTests (both orders), BrokenLiteralScanner guard; tests only (builder).
- 2026-10-25: Done (tech-lead), PR #95 approved and squash-merged. Follow-ups (review nits): the broken-literal scanner
  reads line by line, so a second break on a line that is already broken can be missed (nit 1); C# 11 raw string
  literals (`"""`) are not modelled, and the repository has none today (nit 2).
