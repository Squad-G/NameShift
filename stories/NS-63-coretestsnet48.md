# NS-63: Run the engine tests on .NET Framework 4.8 too

Status: In Review
Depends on: NS-1, NS-28
Area: engine

## Why
`NameShift.Core` runs inside the app on .NET Framework 4.8, but its tests run only on net10.0, so a difference between the
two runtimes (Unicode data, string and culture behaviour, regex) would reach users unseen. `NameShift.Disk.Tests` and
`NameShift.Ui.Tests` already run on both.

## Acceptance criteria
1. Given `tests/NameShift.Core.Tests/NameShift.Core.Tests.csproj`, then it targets `net10.0;net48`
   (`TargetFrameworks`) and sets `<LangVersion>latest</LangVersion>`, like `NameShift.Ui.Tests`; nothing else changes
   there (same packages and versions, `Nullable`, `TreatWarningsAsErrors`).
2. Given `dotnet build -c Release` and `dotnet test -c Release --no-build`, then both pass with no warnings, and the test
   run shows every Core test once per target framework (the two counts are equal).
3. Expected: none. On `dev` before NS-28, the whole Core suite (640 tests) builds and passes on net48 with only the two
   property changes of criterion 1 (checked in the PR #39 review), so the builder must not invent a case. Only if a test
   does fail only on net48 (for example one of NS-28's), the cause is decided case by case in this story, never by deleting or
   skipping the test: (a) if users would see the difference (the app runs on net48), the rule is fixed in
   `NameShift.Core` so both runtimes agree, with the test kept as it is; (b) if the difference is only in the runtime's
   Unicode data for a rare character, the test uses a character that both runtimes classify the same way, and a line in the
   test says why; (c) if the difference is only in the wording of a message a runtime produces (a regex parser message),
   the test checks that it is not empty, not its text. Each such case is listed in the PR.
4. Given tests that hold a path or a file system, then none is added or changed to make them pass; Core tests do not touch
   the disk. (Check by search: `System.IO.File`, `Directory` and `Path.GetTempPath` do not appear in the project.)
5. Given the test runs, then `NameShift.Core.Tests` needs no `#if` on the target framework, unless one test is
   unavoidably different (then it is named in the PR).
6. Given the text that describes the test projects: `CLAUDE.md` lists `tests/NameShift.Core.Tests` as `net10.0, xUnit`
   (the repo has no CI files today), then the builder changes it to `net10.0 and net48, xUnit` in this story's own PR (the
   builder's brief allows this one line; the `po` does not edit `CLAUDE.md`); `README.md` is checked for the same text.

## Out of scope
- New product behaviour (except the fixes of criterion 3a).
- Running the tests on other .NET versions; code coverage.

## Decisions
- Fix the code or the test? **Decision:** the code when a user could see it (the app runs on net48), the test when only the
  runtime's data or message wording differs, never by skipping (po, 2026-10-24).
- Places most likely to differ (for the builder to look at first): the Unicode categories and `Normalize` in
  `RemoveClassesRule`, `CaseRule` title case and `char.IsLetterOrDigit`, `RemoveTextRule` whole-word tests, the regex
  tests of NS-24 (message text, timeout type), and any `InlineData` string holding a lone surrogate (NS-64) (po,
  2026-10-24).
- When to build it? **Decision:** after NS-24 and NS-28 are merged, so their tests are checked on net48 in the same
  run (po, 2026-10-24).

## Log
- 2026-10-24: created (po), from a review finding; set Ready, build after NS-24 and NS-28.
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #42.
