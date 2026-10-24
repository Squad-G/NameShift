# NS-22: Remove: digits, symbols, accents, high characters

Status: Ready
Depends on: NS-1
Area: engine

## Why
Strip whole classes of characters from the name.

## Acceptance criteria
`RemoveClassesRule(bool digits = false, bool symbols = false, bool accents = false, bool highCharacters = false)`,
stage Remove (5), base name only (the extension is never touched). `Enabled` is true when at least one option is on.
`Apply` never throws. The accents part runs first, then digits, symbols and high characters in one pass over the
result.
1. Given `a1b2.txt` and digits, then `ab.txt`. Digits are the characters `char.IsDigit` accepts (Unicode decimal
   digits: `0`-`9`, and for example Arabic-Indic `٣`), not other numerals such as `½` or `Ⅳ`.
2. Given `a!b@c#.txt` and symbols, then `abc.txt`. Symbols are the Unicode punctuation and symbol categories
   (`P*` and `S*`: `!`, `@`, `#`, `-`, `_`, `.`, `+`, `€`, `©`), never letters, digits, spaces or combining marks. A
   dot inside the base name goes too (`a.b.txt` has the base `a.b`, so `ab.txt`); the extension's dot stays.
3. Given `café.txt` (with the precomposed `é` U+00E9) and accents, then `cafe.txt`; given `cafe` + U+0301 (combining
   acute), then `cafe.txt` as well. Letters are decomposed (Unicode FormD), the non-spacing marks (category `Mn`) are
   dropped, and the rest is recomposed (FormC). `ß`, `ø`, `đ` and `æ` have no decomposition and stay as they are.
4. Given a name with no accent, then the accents part leaves the text exactly as it was: a name already in
   decomposed form with no marks is not normalised, and the result is `Unchanged` when nothing else applies.
5. Given a name with a lone surrogate (an unpaired half of an emoji, possible in an NTFS name) and accents, then the
   accents part does nothing and does not throw (`string.Normalize` throws on it: the rule catches that case before
   it can), and the other options still apply.
6. Given `a€b.txt` and high characters, then `ab.txt`. High characters are everything above U+007F; a character
   outside the BMP (an emoji, two UTF-16 units) is removed whole, and the result never holds a lone surrogate.
7. Given accents and high characters together on `café€.txt`, then `cafe.txt` (accents first: the `e` stays).
   Given high characters alone, `café.txt` gives `caf.txt`.
8. Given digits and symbols together on `a_1-2.txt`, then `a.txt`.
9. Given a result that is empty, then a file keeps its extension (`123.txt` with digits gives `.txt`, a valid name per
   NS-5, not flagged) and a folder `123` gives an empty name that the invalid-name check flags `EmptyName`
   (pipeline test with `InvalidNameCheck`).
10. Given the extension, then it is never changed: `a1.mp3` with digits gives `a.mp3`.
11. Given no option on, then `Enabled` is false and the name is unchanged.
12. Given a hidden dotfile `.gitignore` with symbols, then `gitignore` (the whole name is the base, NS-1).

Symbols and digits are only guaranteed for BMP characters (the listed cases). For a character outside the BMP, the
rule keeps it whole or removes it whole, never half, whatever the options; whether symbols removes it depends on the
runtime's Unicode data (the tests run on net10.0, the app on .NET Framework 4.8), so no test asserts that case.

## Out of scope
- The panel (NS-30).
- Transliteration (`ø` to `o`, `ß` to `ss`): see Decisions.
- Removing letters of a given script, or other numerals.

## Decisions
- Transliterate `ø` to `o`? **Decision:** no, only Unicode decomposition, because a hand-written table invites
  mistakes (po, 2026-10-18).
- Does "symbols" include the underscore, the hyphen and the dot? **Decision:** yes, they are punctuation; the panel
  is for stripping noise, and users who want to keep one can use the Remove characters or Replace rules instead
  (po, 2026-10-23).
- Digits: only `0-9`? **Decision:** Unicode decimal digits (`char.IsDigit`), so other scripts behave the same as
  ASCII; one rule, no table (po, 2026-10-23).
- Empty result on a file? **Decision:** `.txt` is valid (NS-5 decided it on purpose); only a folder is flagged
  `EmptyName` (po, 2026-10-23).
- Normalisation form? **Decision:** FormD then FormC, only when a mark was removed, so untouched names are never
  rewritten (po, 2026-10-23).

## Log
- 2026-10-18: created (po).
- 2026-10-23: criteria made testable (classes defined, order, lone surrogate, normalisation), set Ready (po).
