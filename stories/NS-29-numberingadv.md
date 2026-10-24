# NS-29: Numbering rule: hex, letters and roman numerals

Status: In Progress
Depends on: NS-17
Area: engine

## Why
Number files with `0A`, `b` or `IV` instead of only `1`, `2`, `3`. (Restarting the numbers in each folder is NS-60.)

## Acceptance criteria
A new public enum `NumeralSystem { Decimal, Hexadecimal, Letters, Roman }` in Core, and two optional parameters added at
the end of the `NumberingRule` constructor: `NumeralSystem numerals = Decimal, bool? upperCase = null`. `Start`, `Step`
and the item's number stay plain integers (the value); only the way a value is written changes. `upperCase` null means the
system's own default: upper case for Hexadecimal and Roman, lower case for Letters (for Decimal it has no effect). A new
public static class `NumberText` has `string Format(long value, NumeralSystem numerals, int pad, bool upperCase)` and
`bool TryParse(string? text, NumeralSystem numerals, out int value)`; the rule uses `Format`, and the Numbering panel
(NS-61) uses `TryParse` to read a start value typed in the chosen system. Decimal with the default parameters is
exactly what NS-17 built (its tests stay as they are). Existing rule behaviour is unchanged: a value below zero is the
error `The number is below zero.` in every system, checked before the number is written; the separator, the modes and
the insert position work the same.

Writing (`Format`, and the rule):
1. Hexadecimal, Prefix, pad 2, separator `_`, start 10, on `a.txt` and `b.txt`, then `0A_a.txt` and `0B_b.txt`. Start 16
   with pad 0 gives `10` and `11`. Start 255 gives `FF`; with `upperCase: false`, `ff`. Start 10 with step 6 gives `A`
   then `10`. `int.MaxValue` gives `7FFFFFFF`. The pad is the minimum width, filled with `0`, and is never cut.
2. Letters: the values 1, 2, ... 26, 27, 28 are `a`, `b`, ... `z`, `aa`, `ab`; 52 is `az`, 53 is `ba`, 702 is `zz`, 703
   is `aaa`; with `upperCase: true`, `AA`. Prefix, separator `-`, start 26 on three files `x.txt` gives `z-x.txt`,
   `aa-x.txt`, `ab-x.txt`.
3. Roman: 1 `I`, 4 `IV`, 9 `IX`, 14 `XIV`, 40 `XL`, 90 `XC`, 400 `CD`, 1994 `MCMXCIV`, 3999 `MMMCMXCIX`; with
   `upperCase: false`, `iv`. Start 4 on two files gives `IV` then `V`.
4. The pad is ignored in Letters and Roman (value 1, pad 3 gives `a` and `I`).
5. A value that cannot be written is an error of that item only, and its name is kept. The texts are
   `The number cannot be written in letters (from 1).` (Letters, value below 1) and
   `The number cannot be written in Roman numerals (1 to 3999).` (Roman, value below 1 or above 3999). `Format` throws
   `InvalidOperationException` with that text, and the pipeline turns it into the item's `Error` (NS-1). Test: Roman, start 3998 on three files, then the first two get `MMMCMXCVIII` and
   `MMMCMXCIX` and the third (4000) has an error; Letters, start 0 on two files, then the first has an error and the
   second gets `a`.
6. Insert mode works with every system: `abcdef.txt`, Hexadecimal, insert position 2, start 255, no separator, then
   `aFFbcdef.txt`.
7. Defaults: `Numerals` is `Decimal` and `UpperCase` is false; for Hexadecimal and Roman without `upperCase`, `UpperCase`
   is true; for Letters, false; an explicit value is kept. The properties `Numerals` and `UpperCase` return these
   effective values.

Reading (`TryParse`; surrounding white space is ignored, any other white space inside makes it fail; it never throws):
8. Decimal: `12` is 12, `007` is 7, `0` is 0; `-1`, `+1`, `1.5`, `abc`, an empty text and `2147483648` fail.
9. Hexadecimal: `A` and `a` are 10, `FF` is 255, `10` is 16, `000A` is 10, `7FFFFFFF` is `int.MaxValue`; `0x10`, `G`, `-A`,
   an empty text and `80000000` fail.
10. Letters, not case sensitive: `a` is 1, `Z` is 26, `aa` is 27, `Ab` is 28, `zz` is 702; `a1`, `é`, an empty text and a
    word so long that it exceeds `int.MaxValue` (`zzzzzzzzzzzz`) fail.
11. Roman, not case sensitive and only the standard written form: `IV` and `iv` are 4, `MCMXCIV` is 1994, `MMMCMXCIX` is 3999;
    `IIII`, `IC`, `VX`, `MMMM`, `IVX`, `abc` and an empty text fail.
12. Round trip: for every value from 1 to 3999 in Roman, 1 to 3000 in Letters, 0 to 3000 in Hexadecimal and Decimal,
    and for `int.MaxValue` in Hexadecimal, Decimal and Letters, `TryParse(Format(v))` gives `v` (one test loops,
    for both cases).

Edge cases of `NumberText` (called directly):
12a. `Format` with a negative value, an undefined `numerals` or a pad outside 0..255 throws `ArgumentOutOfRangeException`
    (the rule never passes them: it checks the value first and the constructor checks the pad). Value 0 is fine in
    Decimal (`0`) and Hexadecimal (`0`, pad 2 gives `00`); in Letters and Roman it throws `InvalidOperationException`
    (criterion 5), as do Roman 4000 and `long.MaxValue`; Decimal and Hexadecimal accept `long.MaxValue`
    (`9223372036854775807`, `7FFFFFFFFFFFFFFF`). Letters accept `long.MaxValue` without overflow or an endless loop.
12b. `TryParse` with an undefined `numerals` throws `ArgumentOutOfRangeException`; with leading zeros of any length
    (`0000000000007` in Decimal, `00000000000A` in Hexadecimal) it gives 7 and 10; with non-ASCII digits (the Arabic-Indic
    `٣` or full-width `３`) it fails in Decimal and Hexadecimal; Roman `0` and `MMMM` fail; a null text fails.

Construction:
13. Given an undefined `numerals`, the constructor throws `ArgumentOutOfRangeException`; the pad range and the other
    checks of NS-17 are unchanged.
14. Given the pipeline with `AddRule(suffix: "-x")` (stage 7) and this rule (Roman, Suffix, separator `_`, stage 9) on
    `a.txt` and `b.txt`, then `a-x_I.txt` and `b-x_II.txt`.

## Out of scope
- Restarting per folder (NS-60) and the panel options (NS-61).
- Other systems (binary, octal, Greek, other alphabets) and the numbers above 3999 in Roman (a vinculum is not used in
  names).
- A start value typed in the system: the panel reads it (NS-61); the rule always takes the integer value.

## Decisions
- Start typed in which system? **Decision:** the Numbering panel reads the start in the chosen system (hex `FF`, letters
  `ab`, roman `IV`) through `NumberText.TryParse`; the rule keeps an integer, so presets and the command line can store
  either (po, 2026-10-24; the first version of this story had the same intent).
- Case defaults? **Decision:** upper for hex and Roman (`0A`, `IV`, the usual look), lower for letters (`a`, `b`, as in
  the first version of this story); `upperCase` overrides (po, 2026-10-24).
- Pad for letters and Roman? **Decision:** ignored, because zero-filling a letter makes no sense (po, 2026-10-24).
- A value that has no letters or Roman form? **Decision:** a per-item error, as for a value below zero (NS-17), never a
  silent clamp (po, 2026-10-24).
- Letters after `z`? **Decision:** `aa`, `ab`, as spreadsheet columns, because the story asked for it from the start
  (po, 2026-10-24).
- Roman form? **Decision:** only the standard subtractive form is read and written (`IV`, never `IIII`), so `TryParse`
  and `Format` agree (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: break per folder split out to NS-60 (it needs the pipeline to tell a rule which folder an item is in); the rest made testable, set Ready (po).
- 2026-10-24: In Progress (builder).
