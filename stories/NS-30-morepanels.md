# NS-30: Panel for Remove

Status: In Progress
Depends on: NS-11, NS-20, NS-21, NS-22, NS-23
Area: app

## Why
Use the four Remove rules (position, text, character classes, tidy) from the window, in one panel. (The Name and
Extension panels were NS-55.)

## Acceptance criteria
Same pattern as NS-12 and NS-55: a model in `NameShift.Ui.Rules` (extends `RulePanelModel`, stage Remove), a thin
`UserControl` in `src/NameShift.App/Controls/`, one line in `RulePanels.Create()`, every string in
`NameShift.Ui.Strings`. The host accepts one rule per panel, so a small Core class combines the four parts.

In `NameShift.Core` (tests in `NameShift.Core.Tests`):
1. `RemoveGroupRule(IEnumerable<IRenameRule> parts)`, stage Remove (5): applies each enabled part in the given order,
   each on the result of the previous one; `Enabled` is true when at least one part is enabled. Given a part whose
   stage is not Remove, a null part or a null list, then the constructor throws (`ArgumentException`,
   `ArgumentNullException`). Given no parts, then `Enabled` is false and `Apply` returns the name unchanged.
2. Given the parts `RemoveTextRule(characters: "x")` then `RemovePositionRule(first: 1)` on `xab.txt`, then `b.txt`;
   given the same two parts in the other order, then `ab.txt`. The group never reorders its parts. A part that is
   not enabled is skipped (a disabled `RemovePositionRule()` in the list changes nothing).

In `NameShift.Ui` (tests in `NameShift.Ui.Tests`):
3. Given the panel model, then it has these settings, all plain public properties (the presets and command-line
   policy of NS-16 and NS-55): Position: `FirstText`, `LastText`, `FromText`, `ToText` (strings as typed). Text:
   `Characters`, `Text`, `WholeWord`, `MatchCase`, `CropBefore`, `CropAfter`, `KeepMarker`. Classes: `Digits`,
   `Symbols`, `Accents`, `HighCharacters`. Tidy: `Trim`, `LeadingDots`, `DoubleSpaces`. Defaults are empty text and
   false. `IsDefault` is true only when every setting is at its default. The panel is `Enabled` (checked) false by
   default.
4. Given settings, then `BuildRule` returns a `RemoveGroupRule` whose parts, in this order, are: a
   `RemovePositionRule` (from `TextPosition.Parse` of the four position texts), a `RemoveTextRule`, a
   `RemoveClassesRule` and a `RemoveTidyRule`; a part is included only when its own `Enabled` is true. It returns null
   when no part is enabled. Test with one setting at a time (each reaches the right rule with the right value), and
   all together.
5. Given the order, then position runs first (its counts refer to the name the user sees), then text, then classes,
   then tidy last (it cleans what the others leave). Tests through the built rule: `12ab.txt` with `FirstText` = 1
   and `Digits` gives `ab.txt` (position first: the `1` is cut, then the `2` is removed; the other order would give
   `b.txt`); ` x a.txt` with `Characters` = `x` and `Trim` gives `a.txt` (trim after the removal; trim first would
   leave ` a.txt`); `IMG_ Photo 1.jpg` with `FirstText` = 4, `Digits` and `Trim` gives `Photo.jpg`.
6. Given an unusable position text (`0`, `-2`, `abc`, `1.5`), then the model marks it (`FirstInvalid`, `LastInvalid`,
   `FromInvalid`, `ToInvalid`) and `BuildRule` skips that part of the position rule without throwing. A text is
   invalid when it is not empty or white space and `TextPosition.Parse` gives null. For `First` and `Last`, that is the
   whole rule. For the range, one rule: it applies only when both texts parse and From is not after To; when it
   cannot apply and at least one of the two texts is typed (not empty or white space), then both `FromInvalid` and
   `ToInvalid` are true; when both are empty, neither is. Examples: From `abc` with To `5`, then both marked (and the
   reverse: From `5` with To `abc`); From `3` with To empty, then both marked; From `5` with To `2`, then both
   marked; From `2` with To `5`, then neither.
7. Given `Characters` or `Text` typed with white space around it, then it is passed as typed (a space can be the
   thing to remove); an empty value means no part.
8. Given the options that need a text, then the model exposes `WholeWordApplies` (true when `Text` is not empty),
   `MatchCaseApplies` (true when `Characters`, `Text`, `CropBefore` or `CropAfter` is not empty) and
   `KeepMarkerApplies` (true when `CropBefore` or `CropAfter` is not empty); the controls are disabled when false and
   keep their value. A test checks the three properties.
9. Given reset, then every setting returns to its default, the panel is unchecked and `Changed` fires once (the NS-11
   behaviour). Given one setting changed, then `Changed` fires once for it.
10. Given the model, then `Stage` is `RuleStage.Remove`, and a `RulePanelHostModel` built from all seven models
    (Replace, Case, Remove, Add, Numbering, Name, Extension, in any order given) lists them in pipeline order: Name (2),
    Replace (3), Case (4), Remove (5), Add (7), Numbering (9), Extension (11).
11. Given the panel enabled with only default settings, then `BuildRule` returns null and the preview is unchanged
    (nothing breaks).
12. Given a pipeline test with the panel enabled and the Replace panel enabled, then Replace (stage 3) runs before
    Remove (stage 5) on the same name.
13. Mnemonics (per panel, as decided; the test from NS-16): title "R&emove" (E); Position: "&First characters" (F),
    "&Last characters" (L), "Fro&m position" (M), "&To position" (T); Text: "C&haracters" (H), "Te&xt" (X),
    "&Whole word" (W), "Match &case" (C), "Crop &before" (B), "Crop &after" (A), "&Keep the marker" (K); Classes:
    "&Digits" (D), "&Symbols" (S), "Acce&nts" (N), "Hi&gh characters" (G); Tidy: "T&rim spaces" (R), "Leading d&ots"
    (O), "Double s&paces" (P). All 19 letters are distinct inside the panel and none repeats the title's E. The panel
    titles then are R, C, E, A, N, M, X (Replace, Case, Remove, Add, Numbering, Name, Extension), all distinct. The
    four group captions ("Position", "Text", "Character types", "Tidy") have no mnemonic.

In the window (by hand, listed in the PR):
14. The panel groups its settings under the four captions in the order Position, Text, Character types, Tidy; invalid
    position fields show the error icon and an accessible description, not colour alone (the `RulePanelLayout.Mark`
    helper of NS-53); the tab order follows the visual order; the panel fits at 100 % and 200 % scale (the stack
    scrolls when the window is small); a screen reader reads the title, each label and the disabled state.

## Out of scope
- Saving in presets (NS-36) and command-line options (NS-40): the model keeps plain properties; the story that builds
  them adds the fields.
- A separate panel per Remove rule.
- Changing the four rules (NS-20 to NS-23).

## Decisions
- One panel per rule story? **Decision:** one Remove panel with four groups and one enable checkbox, because there is
  one Remove stage and the host allows one panel per stage (po, 2026-10-23).
- How do four rules fit one panel? **Decision:** a small `RemoveGroupRule` in Core applies them in a fixed order;
  keeping it in Core makes the order testable without the window (po, 2026-10-23).
- Order inside the group? **Decision:** position, text, classes, tidy. Position counts refer to the name shown in the
  list; tidy goes last so it cleans spaces and dots the others leave behind (po, 2026-10-23).
- Invalid fields? **Decision:** marked and the part skipped, never an exception, as in Add (NS-12) and Numbering
  (NS-53); the other parts still apply (po, 2026-10-23).
- Disabled options? **Decision:** Whole word, Match case and Keep the marker are greyed when no text uses them and
  keep their value, the same pattern as the Case exceptions box (po, 2026-10-23).
- Alt letters? **Decision:** unique inside the panel, repeats across panels allowed, panel titles distinct (David,
  2026-10-23); letters in criterion 13.

## Log
- 2026-10-18: created (po).
- 2026-10-23: Name and Extension panels split out to NS-55; this story is now the Remove panel only (po).
- 2026-10-23: criteria made testable (group rule, order, validation, letters), set Ready; build after NS-20 to NS-23 are merged (po).
- 2026-10-24: In Progress (builder).
