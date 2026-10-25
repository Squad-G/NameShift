# NS-80: Fix raw control characters in a test and guard against them

Status: In Review
Depends on:
Area: app
Chore: no product change.

## Why
`tests/NameShift.Ui.Tests/Actions/MainActionsTests.cs`, test `Rename_NeedsAttention_LocksRename` (lines 1114 and 1115 when the story was
written), holds a raw backspace byte (0x08) in `@"C:\A.txt"` of the `Failure` and a raw bell byte (0x07) in the
`@"C:\A.txt"` of the `Stranded` call. A verbatim string `@"C:\A\b.txt"` was meant, and `@"C:\A\a.txt"`: an editor or a
paste turned the `\b` and `\a` into the control characters, so the paths read `C:\A.txt` on screen and are not the paths
the test means. Nothing fails today; the next person to edit the line will not see what is there.

## Acceptance criteria
1. Given the test, then the two literals are `@"C:\A\b.txt"` (the failed item of `DiskResults.Failure`) and
   `@"C:\A\a.txt"` (the second argument of `DiskResults.Stranded`), written with printable characters only (the
   backslash-letter pairs, not escapes that the verbatim string would not process). The test still passes on net10.0 and
   net48; if an assertion depended on the old strings, it is updated to the new paths and the PR says so.
2. Given a guard, then a Ui test (next to `AppStyleScanTests`) reads every `.cs`,
   `.resx`, `.csproj`, `.md`, `.iss`, `.json` and `.props` file under `src`, `tests`, `docs`, `stories` and `installer`
   (a folder that does not exist, such as `installer` today, is skipped) and fails, naming `file:line` and the code point, for any control character other than tab, line feed and carriage
   return (U+0000 to U+0008, U+000B, U+000C, U+000E to U+001F, U+007F). Files under `bin` and `obj` are not scanned. The repository-root finder is private to `AppStyleScanTests`
   today (`RepositoryRoot()`): the builder extracts it into a shared internal test helper used by both scans, with no change
   in what `AppStyleScanTests` checks.
3. Given the scanning logic, then it is a small pure function (text in, list of line and code point out) with its own tests
   that build the offending text from `\u0008`, `\u0007`, `\u0000`, `\u007F` escapes (never raw bytes in the test file,
   which would fail the guard itself) and check: a clean text gives nothing; one finding per control character with the right
   line; tab, `\r\n` and `\n` give nothing; a form feed (U+000C) is found.
4. Given a mutation (the function ignoring U+0007), then the test of criterion 3 fails; the PR says so. With the fix of
   criterion 1 reverted in a scratch copy, the guard of criterion 2 fails and names `MainActionsTests.cs` and line 1114.
5. Given the guard runs on the whole tree, then it finishes in under two seconds on the current repository, and it does not
   fail while another worktree writes elsewhere (it reads only the working tree it runs from).

## Out of scope
- Other encodings (BOM, mixed line endings): not a problem seen; the scan does not judge them.

## Decisions
- A guard for one bug? **Decision:** yes, a ten-line scan: the bug was invisible to a reader and to every tool in use, which
  is exactly what a scan is for (po, 2026-10-24).
- Which paths in the test? **Decision:** `C:\A\b.txt` and `C:\A\a.txt`, as the owner of the test meant (po, 2026-10-24).

## Log
- 2026-10-24: created and set Ready (po). The task note gave lines 1081-1082; the bytes are on 1114 and 1115.
- 2026-10-24: PR #75 review applied: missing folders skipped, the root finder is extracted, stale commit id dropped (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #89.
