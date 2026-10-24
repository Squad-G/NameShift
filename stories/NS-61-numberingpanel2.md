# NS-61: Numbering panel: number system

Status: In Review
Depends on: NS-53, NS-29
Area: app

## Why
Number files with `0A`, `b` or `IV` from the Numbering panel, using the systems of NS-29. (The "restart in each folder"
checkbox comes with the sub-folders option, NS-33, so no option shows that has no effect.)

## Acceptance criteria
Same pattern as NS-53 and NS-59: the logic is in `NumberingPanelModel` (`NameShift.Ui.Rules`), the `UserControl`
`NumberingPanel` stays thin, every string is in `NameShift.Ui.Strings`, every setting a plain public property, tests in
`NameShift.Ui.Tests` (`NumberingPanelModelTests.cs`). The existing NS-53 tests pass unchanged.

Settings:
1. Given the model, then it has `Numerals` (`NumeralSystem`, default `Decimal`) and `UpperCase` (`bool`, default true).
   Assigning an undefined `NumeralSystem` throws `ArgumentOutOfRangeException`. A static list `Systems` gives the choices in
   enum order (Decimal, Hexadecimal, Letters, Roman) and `IndexOf(NumeralSystem)` gives the position, as `Modes` and
   `IndexOf` do for the mode.
2. Given `Numerals` is assigned a different system, then `UpperCase` becomes that system's usual case: Hexadecimal true,
   Roman true, Letters false, Decimal true. Afterwards `UpperCase` can be set freely and is kept until the system changes
   again. The two changes raise `Changed` once in all (together with the start conversion of criterion 3).
3. Given the start as typed in the old system, when `Numerals` changes, then the start text is rewritten in the new system
   if it reads in the old one and can be written in the new one: with `NumberText.Format(value, newSystem, 0, UpperCase)`
   after `UpperCase` has been updated. Otherwise the text stays as typed (and is marked, criterion 5). Tests:
   Decimal `10` to Hexadecimal gives `A`, to Letters `j`, to Roman `X`; Decimal `255` to Hexadecimal gives `FF`, and back
   to Decimal gives `255`; Hexadecimal `A` to Decimal gives `10`; Letters `ab` to Decimal gives `28`; Roman `IV` to
   Hexadecimal gives `4`. Decimal `0` to Letters stays `0`; Decimal `4000` to Roman stays `4000`; Decimal `abc` to
   Hexadecimal stays `abc`; setting the system it already has changes nothing and raises nothing.
4. Given `StartText`, then `Start` (`int?`) reads it in the chosen system. Decimal is unchanged from NS-53: whitespace
   trimmed, `int.TryParse` with an optional sign and the invariant culture, must be 0 or more (`+5` is accepted, `-1` and
   `1.5` are not). The other systems use `NumberText.TryParse` (NS-29): Hexadecimal `FF` and `ff` give 255, `0x10`, `G` and
   `80000000` give null; Letters `a` gives 1, `ab` gives 28, `0` and `a1` give null; Roman `IV` and `iv` give 4, `IIII`,
   `0` and `MMMM` give null. An empty text gives null in every system.
5. Given an unusable start, then `StartError` is `""` when `Start` is not null and otherwise, by system:
   Decimal `Type a whole number from 0. Until then numbering is skipped.` (as today);
   Hexadecimal `Type a hexadecimal number from 0 to 7FFFFFFF (digits 0-9 and A-F). Until then numbering is skipped.`;
   Letters `Type letters from a (a, b, ... z, aa, ab ...). Until then numbering is skipped.`;
   Roman `Type a Roman numeral from I to MMMCMXCIX (1 to 3999). Until then numbering is skipped.`
6. Given `PadApplies`, then it is true for Decimal and Hexadecimal and false for Letters and Roman (the pad has no effect
   there, NS-29). When it is false, `PadError` is `""`, a bad `PadText` does not stop `BuildRule`, the rule gets a pad of 0
   and `PadText` keeps its text. `CaseApplies` is true for Hexadecimal, Letters and Roman and false for Decimal.
7. Given settings, then `BuildRule` returns a `NumberingRule` with `numerals: Numerals` and `upperCase: UpperCase`. Pipeline
   tests with the panel enabled on `a.txt` and `b.txt` (Prefix, separator `_`) unless stated: Hexadecimal, start `A`, pad
   `2` gives `0A_a.txt`, `0B_b.txt`; the same with `UpperCase` false gives `0a_a.txt`; Letters (after choosing it) with
   Suffix, separator `-`, start `z` on `x.txt`, `y.txt`, `z.txt` gives `x-z.txt`, `y-aa.txt`, `z-ab.txt`; Letters with
   `UpperCase` true and start `a` gives `A_a.txt`; Roman, start `IV` gives `IV_a.txt`, `V_b.txt`, and with `UpperCase` false
   `iv_a.txt`; Roman with start `MMMCMXCIX` on two files gives `MMMCMXCIX_a.txt` and an error on `b.txt` (NS-29), not an
   exception; Decimal with the defaults still gives `a_1.txt`, `b_2.txt`. A pad of `4` with Letters gives `a_a.txt`.
8. Given an unusable start, then `BuildRule` returns null and never throws, in every system (Roman with `0`, Hexadecimal
   with `G`, Letters with an empty text).
9. Given `IsDefault`, then it is true only when `Numerals` is Decimal, `UpperCase` is true and every NS-53 setting is at its
   default. Given reset, then every setting returns to its default (Decimal, true, `1`, ...), the panel is unchecked and
   `Changed` fires once.
10. Given the new labels, then the Numbering panel has "Number &system" (S) and "Letter &case" (C) besides the NS-53
    letters N (title), L, V, Y, G, B and H: all distinct inside the panel, none repeating the title's. The mnemonic test of
    `FirstPanelsTests.cs` gains the two labels. The words in the drop-down lists have no mnemonic.

In the window (by hand, listed in the PR):
11. The system drop-down reads "Numbers (1, 2, 3)", "Hexadecimal (0A, 0B)", "Letters (a, b, c)", "Roman numerals (I, II,
    III)" (`NumeralSystemText.For`, tested), and the case drop-down "Capital letters" and "Small letters" (index 0 is
    capital, so `UpperCase` true). The Digits box and the case drop-down are greyed with their labels when they do not apply
    and keep their values. An unusable start shows `StartError` under the box with the error icon and an accessible
    description (`RulePanelLayout.Mark`). Choosing a system rewrites the start box at once. Tab order follows the visual
    order; the panel fits at 100 % and 200 % scale; a screen reader reads the title, each label and the disabled state.

## Out of scope
- The "restart in each folder" checkbox: NS-60 (engine) and NS-33 (sub-folders) first.
- Saving in presets (NS-36) and command-line options (NS-40).
- Typing the step in another system: the step stays a decimal whole number.

## Decisions
- Start typed in which system? **Decision:** the chosen one, read with `NumberText.TryParse`, as NS-29 decided; the rule
  keeps an integer (po, 2026-10-24).
- `+5`: NS-29's `TryParse` refuses it, today's Numbering start box accepts it. **Decision:** Decimal keeps the NS-53 read
  (accepts `+5`) so nothing that worked stops working; only Hexadecimal, Letters and Roman use `TryParse` (po, 2026-10-24).
- Case control? **Decision:** a two-item drop-down, greyed for Decimal where it has no effect (po, 2026-10-24). The
  system's usual case is chosen when the system changes (`0A`, `IV`, `a`), so the first choice looks right at once.
- Switching system with a start typed? **Decision:** keep its value when it can be written in the new system, otherwise
  keep the text and mark it; never clear it silently (po, 2026-10-24).
- Pad for Letters and Roman? **Decision:** greyed, with a bad text ignored, as the Insert position is outside Insert mode
  (NS-53) (po, 2026-10-24).
- Restart checkbox here? **Decision:** no; with one folder listed it does nothing, so it is added with the sub-folders
  option (po, 2026-10-24).

## Log
- 2026-10-24: created when NS-29 was split (po).
- 2026-10-24: restart checkbox left out until NS-33; criteria made testable against the NS-29 story and `NumberingPanelModel`; set Ready (NS-29 is merged) (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #50.
