# NS-31: Panels for RegEx and Move/Copy

Status: Ready
Depends on: NS-11, NS-24, NS-25
Area: app

## Why
Use the RegEx rule (NS-24) and the Move/Copy rule (NS-25) from the window. (Auto date and Append folder name are NS-59,
split out the way NS-55 was split out of NS-30.)

## Acceptance criteria
Same pattern as NS-12, NS-55 and NS-30: a model in `NameShift.Ui.Rules` (extends `RulePanelModel`), a thin `UserControl`
in `src/NameShift.App/Controls/`, one line each in `RulePanels.Create()`, every string in `NameShift.Ui.Strings`
(`Strings.resx` and `Strings.cs`). Tests in `NameShift.Ui.Tests`. Every setting is a plain public property (the
presets and command-line policy of NS-16 and NS-55: NS-36 and NS-40 add the fields later).

RegEx panel (`RegexPanelModel`, stage RegEx, 1):
1. Given the model, then it has `Pattern`, `Replacement` (strings as typed, never trimmed: a space can matter),
   `IgnoreCase` and `IncludeExtension`. Defaults: empty, empty, false, false. `IsDefault` is true only when all four are at
   their default. The panel is `Enabled` (checked) false by default.
2. Given a usable pattern, then `BuildRule` returns a `RegexRule` with `Pattern`, `Replacement`, `IgnoreCase` and
   `IncludeExtension` equal to the settings (one test per setting). Given an empty `Pattern`, then `BuildRule` returns
   null. Given an invalid pattern (`(`, `[a-`, `*a`), then `BuildRule` returns null and does not throw.
3. Given the model, then `PatternError` is `""` when the pattern is empty or valid and otherwise
   `This pattern is not valid. {message} Until then the rule is skipped.` with `{message}` the text
   `RegexRule.Validate` returns (a test checks that the error contains it, not its wording). It is a plain property, so it is
   live: after `Pattern = "("` it is not empty, after `Pattern = "(a)"` it is empty again, and each assignment raises
   `Changed` once.
4. Given reset, then every setting returns to its default, the panel is unchecked and `Changed` fires once (the NS-11
   behaviour). Given one setting changed, then `Changed` fires once for it.
5. Given a `RulePanelHostModel` with the RegEx panel (`IMG_(\d+)` to `Photo $1`) and the Replace panel (`Photo` to `Pic`)
   both enabled, then on `IMG_042.jpg` the result is `Pic 042.jpg`: RegEx (stage 1) runs before Replace (stage 3).
6. Given the panel enabled with default settings, then `BuildRule` returns null and the preview is unchanged.

Move/Copy panel (`MoveCopyPanelModel`, stage MoveCopy, 6):
7. Given the model, then it has `Mode` (`MoveCopyMode`), `Source` (`MoveCopySource`), `CountText`, `Target`
   (`MoveCopyTarget`), `PositionText` and `Separator`. Defaults: Move, Start, empty, End, empty, empty. `IsDefault` is true
   only when all six are at their default. The panel is `Enabled` false by default. Assigning an undefined enum value
   throws `ArgumentOutOfRangeException` (a programmer error, as in the Numbering panel). Static lists `Modes`, `Sources`
   and `Targets` give the choices in the order of the enums, with `IndexOf` helpers like `NumberingPanelModel`.
8. Given `CountText`, then `Count` is `TextPosition.Parse` of it (a whole number from 1; `0`, `-2`, `abc`, `1.5` give
   null). `CountInvalid` is true when the text is not empty or white space and `Count` is null. Given `PositionText`,
   then `Position` is `TextPosition.Parse` of it, `PositionApplies` is true only when `Target` is `Position`, and
   `PositionInvalid` is true when `PositionApplies` and `Position` is null (an empty text too, as the Numbering panel
   marks its position). Error text for both:
   `Type a whole number from 1. Until then nothing is moved or copied.`
9. Given settings, then `BuildRule` returns a `MoveCopyRule` with the same mode, source, count, target and separator
   (never trimmed), and the position only when `PositionApplies`. It returns null when `Count` is null, or when
   `PositionApplies` and `Position` is null, and never throws. Test one setting at a time, and the examples of NS-25:
   `2024report.txt` with Move, Start, `4`, End and `_` gives `report_2024.txt`; `abcdef.txt` with Move, Start, `2`,
   Position and `3` gives `cdabef.txt`.
10. Given the Position choice is not selected, then the position control is disabled and keeps its text, and the rule gets
    no position (a test through `PositionApplies` and `BuildRule`).
11. Given reset, then every setting returns to its default, the panel is unchecked and `Changed` fires once.
12. Given a host with the Remove panel (`FirstText` = `1`), the Move/Copy panel (Move, Start, `CountText` = `2`, End,
    `Separator` = `_`) and the Add panel (`Prefix` = `P-`), all enabled, then `xabcd.txt` gives `P-cd_ab.txt`.

Both panels:
13. Given the host model built from all nine models (Replace, Case, Remove, Add, Numbering, Name, Extension, RegEx,
    Move/Copy, in any order given), then it lists them in pipeline order: RegEx (1), Name (2), Replace (3), Case (4),
    Remove (5), Move/Copy (6), Add (7), Numbering (9), Extension (11).
14. Mnemonics (per panel, as decided, with the test from NS-16): RegEx: title "Re&gEx" (G), "&Pattern" (P),
    "Replace&ment" (M), "&Ignore case" (I), "Include e&xtension" (X). Move/Copy: title "Mo&ve or copy" (V), "Ac&tion" (T),
    "Take &from" (F), "&Characters" (C), "&Put at" (P), "Po&sition" (S), "Text &between" (B). Inside each panel the
    letters are distinct and none repeats the title's. The panel titles then are R, C, E, A, N, M, X, G, V (Replace, Case,
    Remove, Add, Numbering, Name, Extension, RegEx, Move/Copy), all distinct, and the test checks them. The words in the
    drop-down lists have no mnemonic.

In the window (by hand, listed in the PR):
15. The RegEx panel shows `PatternError` under the pattern box with the error icon and an accessible description, not
    colour alone (the `RulePanelLayout.Mark` helper), updated as the user types. The Move/Copy drop-downs read: Action
    "Move", "Copy"; Take from "Start of the name", "End of the name"; Put at "Start of the name", "End of the name", "A
    position". Invalid count and position boxes show the error icon. Tab order follows the visual order; both panels fit
    at 100 % and 200 % scale (the stack scrolls when the window is small); a screen reader reads the title, each label
    and the disabled state.

## Out of scope
- Auto date and Append folder name panels (NS-59).
- Saving in presets (NS-36) and command-line options (NS-40).
- A regex tester, a cheat sheet, saved patterns.
- Changing the two rules (NS-24, NS-25).

## Decisions
- Split? **Decision:** this story keeps RegEx and Move/Copy, and Auto date plus Append folder name move to NS-59: four
  panels with their format lists and examples are too much for one PR (po, 2026-10-24).
- Where is a bad pattern shown? **Decision:** once, under the pattern box, live; the rule is skipped until it is fixed,
  as NS-24 decided (po, 2026-10-24).
- Count box: marked when empty? **Decision:** no (nothing typed yet, like the Remove boxes); the position box is marked
  when it applies and is empty, as in the Numbering panel (po, 2026-10-24).
- Default separator of Move/Copy? **Decision:** empty, because moving characters should not add text unless asked; the
  Numbering panel's `_` is a numbering habit (po, 2026-10-24).
- Alt letters? **Decision:** unique inside the panel, repeats across panels allowed, panel titles distinct (David,
  2026-10-23); letters in criterion 14.

## Log
- 2026-10-18: created (po).
- 2026-10-24: split: Auto date and Append folder panels moved to NS-59; criteria made testable, set Ready; build after NS-24 and NS-25 are merged (po).
