# NS-64: Tests with a lone surrogate must really use one

Status: Ready
Depends on: NS-21
Area: engine

## Why
xUnit stores `[InlineData]` strings as UTF-8, so a lone surrogate there (`"\uD800"`) turns into the replacement
character U+FFFD and the test checks nothing about surrogates. Such tests must build the string in the test body.

## Acceptance criteria
1. Given `Apply_NeverThrows_OnOddValues` in `tests/NameShift.Core.Tests/RemoveTextRuleTests.cs` (the theory with
   `[InlineData("\uD800")]` and `[InlineData("\0")]`), then it becomes `[Fact]`s that use C# literals: one for the lone
   high surrogate `"\uD800"`, one for the lone low surrogate `"\uDC00"`, one for `"\0"`. Each keeps the assertions of the
   current test (the rule does not throw, and the result has at most one lone surrogate; none for the emoji input). If the
   new low-surrogate fact fails because of a real defect in `RemoveTextRule`, the builder does not loosen the test: the PR
   says so and the lead decides (a fix in the rule is then its own change).
2. Given every `[InlineData]`, `[ClassData]` and `[MemberData]` in `tests/` (the three projects `NameShift.Core.Tests`,
   `NameShift.Disk.Tests` and `NameShift.Ui.Tests`), then none supplies a string holding a lone surrogate (an escape
   from `\uD800` to `\uDFFF` that does not form a pair, or the same as a `\x` escape). The search made for this story
   (`InlineData\(.*\\u[dD][89a-fA-F]`) found only the instance in criterion 1; the builder repeats it, also for
   `MemberData` and `ClassData` sources, and lists the result in the PR. Any other one found is rewritten the same way.
3. Given a lone surrogate used inside a `[Fact]` body as a C# literal, then it is left as it is (the compiler keeps it in
   memory; those tests are valid).
4. Given the new facts, then a comment above them says why they are not `InlineData` (UTF-8 turns a lone surrogate into
   U+FFFD).
5. Given the new tests, then each one fails when `RemoveTextRule` is changed to throw on a lone surrogate (the builder
   checks it once by hand and says so in the PR); `dotnet build -c Release` and `dotnet test -c Release --no-build` pass.

## Out of scope
- Changing any rule.
- Adding a test helper or an analyser to forbid this later.

## Decisions
- Fix the attribute or the data? **Decision:** rewrite as `[Fact]`s with literals: the data cannot be saved in an
  attribute, so no `[InlineData]` workaround exists (po, 2026-10-24).

## Log
- 2026-10-24: created (po), from a review finding; set Ready.
