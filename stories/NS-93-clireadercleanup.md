# NS-93: Command line readers: remove the copies

Status: In Progress
Depends on: NS-40, NS-69, NS-75, NS-76
Area: app
Chore: no visible change; every existing Cmd test passes unchanged (except the one fixed row of criterion 5).

## Why
The review of the CLI stories found the same few lines written several times in the readers. One place each keeps the next
readers (NS-41 and NS-87 add one) short and the error texts the same.

## Acceptance criteria
`NameShift.Cmd` only. Behaviour, messages, exit codes and the order of checks stay identical; the whole existing Cmd test
suite passes without edits to its expectations.
1. Given `NumberingReader`, then `--number-mode`, `--number-system` and `--number-case` use `OptionArgs.TakeChoice` (the
   order is the same: a missing value, then a repeat, then a value outside the choices) and the reader's own three copies of
   that code are gone. A test per option checks the three errors (`OptionNeedsValue`, `RepeatedOption`, `BadChoice` with the
   values text) if no existing test does; the PR names which tests already did.
2. Given `AutoDateReader.ReadSwitch` and `FolderNameReader.ReadSwitch` (the same code), then there is one helper in
   `OptionArgs` (indicative `TakeSwitch(ref bool target, string option, out ParsedCommand? error)`) that both call, and the
   switch reads in `NumberingReader` and `RunSwitchesReader` use it where they have the same shape.
3. Given the `ReadOwn` methods that end with `return ok ? OptionRead.Taken : OptionRead.Failed(error!)`, then that ending is
   one helper (indicative `OptionRead.From(bool ok, ParsedCommand? error)`), and `ReadOwn` no longer repeats the
   "if not ok return Failed" chain.
4. Given `CommandRunner`, which writes `NameShift.Ui.Strings.ActionUnexpected` in full because `NameShift.Cmd.Strings` has the
   same name, then a `using` alias (`UiStrings`) is used in every file of `NameShift.Cmd` that needs both, and no
   fully qualified `NameShift.Ui.Strings` remains in `src/NameShift.Cmd`.
5. Given `RegexMoveDateFolderParserTests` (the member `ErrorRows` feeds the theory `UsageErrors`), then the row
   `--copy|1|--copy|1` (written at line 112) is also yielded by the loop over `Words` (`Once("--copy")` is `--copy|1`), so
   xUnit sees two identical rows and silently skips one. The hand-written row at line 112 is removed (the loop keeps it),
   and a test fails when `ErrorRows` yields the same row twice (a check on the member's rows, in the same file).
6. Given the checks (`dotnet build -c Release` and `dotnet test -c Release --no-build`), then they pass on both runtimes, and
   the number of test cases is the old number minus the removed duplicate (written in the PR).

## Out of scope
- New options; renaming the options; changing the help text.

## Decisions
- One concern per PR? **Decision:** yes: these four cleanups are all about the readers' shared helpers and ship together; the
  duplicate test row rides along because it is in the same files' tests (po, 2026-10-25).
- Is the duplicate row a real finding? **Decision:** yes, confirmed in the PR #90 review (po, 2026-10-25).
- Order against NS-41? **Decision:** NS-93 first: the new filter reader then starts from the shared helpers and there are
  fewer merge conflicts in the readers (po, 2026-10-25).

## Log
- 2026-10-25: created from the CLI review follow-ups; set Ready (po).
- 2026-10-25: In Progress (builder).
