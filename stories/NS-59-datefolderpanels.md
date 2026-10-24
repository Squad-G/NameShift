# NS-59: Panels for Auto date and Append folder name

Status: In Progress
Depends on: NS-11, NS-26, NS-28, NS-31
Area: app

## Why
Use the Auto date rule (NS-26) and the Append folder name rule (NS-28) from the window. With NS-31 this completes the
eleven panels of docs/PLAN.md.

## Acceptance criteria
Same pattern as NS-31: a model in `NameShift.Ui.Rules` (extends `RulePanelModel`), a thin `UserControl` in
`src/NameShift.App/Controls/`, one line each in `RulePanels.Create()`, every string in `NameShift.Ui.Strings`, tests in
`NameShift.Ui.Tests`, every setting a plain public property.

Auto date panel (`AutoDatePanelModel`, stage AutoDate, 8):
1. Given the model, then it has `Source` (`DateSource`), `Format` (string as typed, never trimmed), `Placement`
   (`DatePlacement`) and `Separator`. Defaults: Modified, `yyyy-MM-dd`, Prefix, `_`. `IsDefault` is true only when all four
   are at their default. The panel is `Enabled` false by default. Assigning an undefined enum value throws
   `ArgumentOutOfRangeException`. Static lists `Sources` and `Placements` give the choices in enum order, with `IndexOf`
   helpers like `NumberingPanelModel`.
2. Given a usable format, then `BuildRule` returns an `AutoDateRule` with the same source, format, placement and
   separator (one test per setting). Given an empty format, then it returns null. Given an invalid format (`'abc`,
   `yyyy\`), then it returns null and does not throw.
3. Given the model, then `FormatError` is `""` when the format is empty or valid and otherwise
   `This format is not valid. {message} Until then the date is skipped.` with `{message}` the text
   `AutoDateRule.Validate` returns. It is a plain property (live), and each assignment of `Format` raises `Changed` once.
4. Given the model, then `Example` is the text `AutoDateRule.FormatDate` gives for the fixed sample date
   `AutoDatePanelModel.SampleDate` (2024-03-09 14:05:07), or `""` when the format is empty or invalid. `yyyy-MM-dd` gives
   `2024-03-09`, `yyyyMMdd_HHmmss` gives `20240309_140507`, `MMMM` gives `March` (also when the test sets the current
   culture to `fr-FR`), `d` gives `9`.
5. Given the static list `CommonFormats` (`yyyy-MM-dd`, `yyyyMMdd`, `yyyy-MM-dd_HH-mm-ss`, `yyyyMMdd_HHmmss`,
   `dd-MM-yyyy`, `yyyy-MM`, `yyyy`), then every entry passes `Validate`, none gives an example holding a character Windows
   forbids in file names, and all examples are different (a test checks it).
6. Given reset, then every setting returns to its default, the panel is unchecked and `Changed` fires once. Given the panel
   enabled with only default settings, then `BuildRule` still returns a rule (the defaults are a usable setting, as in
   the Numbering panel) and `IsDefault` is true.

Append folder name panel (`FolderNamePanelModel`, stage FolderName, 10):
7. Given the model, then it has `Placement` (`FolderNamePlacement`), `LevelsText` (as typed) and `Separator`. Defaults:
   Prefix, `1`, `_`. `IsDefault` is true only when all three are at their default. The panel is `Enabled` false by default.
   Assigning an undefined placement throws `ArgumentOutOfRangeException`. A static list `Placements` and an `IndexOf`
   helper, as above.
8. Given `LevelsText`, then `Levels` is `TextPosition.Parse` of it (a whole number from 1; `0`, `-1`, `abc`, an empty text
   give null; a number too large to store is `int.MaxValue`, which NS-28 clamps). `LevelsInvalid` is true when `Levels` is
   null (an empty text too, since the field has a default of 1). Error text:
   `Type a whole number from 1. Until then the folder name is skipped.`
9. Given settings, then `BuildRule` returns a `FolderNameRule` with the same placement, levels and separator (never
   trimmed), and returns null when `Levels` is null, without throwing. One test per setting.
10. Given reset, then every setting returns to its default, the panel is unchecked and `Changed` fires once.

Both panels:
11. Given the host model built from all eleven models (any order given), then it lists them in pipeline order: RegEx (1),
    Name (2), Replace (3), Case (4), Remove (5), Move/Copy (6), Add (7), Auto date (8), Numbering (9), Append folder name
    (10), Extension (11). The second sentence is a hand check, not a test: `RulePanels.Create()` in the App
    returns exactly these eleven entries (the App has no test project); the PR lists it under the hand checks.
12. Given the item `a.jpg` in `C:\Photos\Paris`, modified 2024-03-09 14:05:07, and a host with the Add panel (`Suffix` =
    `-x`), the Auto date panel (`Placement` Suffix), the Numbering panel (Suffix, defaults) and the Append folder name
    panel (`Placement` Suffix), all enabled, then the result is `a-x_2024-03-09_1_Paris.jpg`.
13. Mnemonics (per panel, as decided; test from NS-16): Auto date: title "Auto &date" (D), "&Source" (S), "&Format" (F),
    "&Place" (P), "Text &between" (B). Append folder name: title "Append &folder name" (F), "&Place" (P), "Folder &levels"
    (L), "Text &between" (B). Inside each panel the letters are distinct and none repeats the title's. The eleven panel
    titles then are R, C, E, A, N, M, X, G, V, D, F (Replace, Case, Remove, Add, Numbering, Name, Extension, RegEx,
    Move/Copy, Auto date, Append folder name), all distinct. The same two tests of `FirstPanelsTests.cs` (named in NS-31
    criterion 14) gain the two panels and the letters `D F`. The words in the drop-down lists
    and the example label have no mnemonic.

In the window (by hand, listed in the PR):
14. The Auto date panel shows the source and placement as drop-downs ("Modified date", "Created date"; "Before the name",
    "After the name"), the format in an editable drop-down list of `CommonFormats` (typing a custom format is allowed;
    picking one sets the text), and a label `Example: {Example}` that updates as the user types; an invalid format shows
    `FormatError` under the box with the error icon and an accessible description (the `RulePanelLayout.Mark` helper). The
    Append folder name panel shows the placement as a drop-down ("Before the name", "After the name"). Tab order follows
    the visual order; the panels fit at 100 % and 200 % scale (the stack scrolls); a screen reader reads the title, each
    label, the example and the error.

## Out of scope
- The photo date source (NS-62).
- Saving in presets (NS-36) and command-line options (NS-40).
- Showing the example for the selected file (the example uses a fixed sample date).
- Changing the two rules (NS-26, NS-28).

## Decisions
- Common formats and a custom one? **Decision:** one editable drop-down: the list gives the common ones, typing gives a
  custom one, so one `Format` string holds it (po, 2026-10-24).
- Example? **Decision:** a fixed sample date, shown live, so the user sees what the letters do without a selected file;
  it uses the rule's own formatter so it cannot disagree (po, 2026-10-24).
- Common formats avoid `:`? **Decision:** yes, no character Windows forbids; criterion 5 tests it (po, 2026-10-24).
- Enabled with defaults? **Decision:** the Auto date and Append folder panels have a usable default (`yyyy-MM-dd`, one
  level), so enabling them acts at once, like the Numbering panel (po, 2026-10-24).
- Alt letters? **Decision:** unique inside the panel, repeats across panels allowed, panel titles distinct (David,
  2026-10-23); letters in criterion 13.

## Log
- 2026-10-24: created when NS-31 was split (po).
- 2026-10-24: criteria made testable, set Ready; build after NS-26, NS-28 and NS-31 are merged (po).
- 2026-10-24: In Progress (builder).
