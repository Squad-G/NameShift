# NS-25: Move or copy parts of the name

Status: Ready
Depends on: NS-1
Area: engine

## Why
Reorder a name by moving or copying a few characters, for example `2024report.txt` to `report_2024.txt`.

## Acceptance criteria
Three new public enums in Core: `MoveCopyMode { Move, Copy }`, `MoveCopySource { Start, End }` and
`MoveCopyTarget { Start, End, Position }`, and
`MoveCopyRule(MoveCopyMode mode = Move, MoveCopySource source = Start, int count = 0, MoveCopyTarget target = End,
int? position = null, string separator = "")`, stage MoveCopy (6: after Remove, before Add). Only the base name
changes; the extension is never touched.

How it works: the rule takes `count` characters from the start or the end of the base name (the "taken text"). Move
cuts the taken text out and keeps the rest (the "remainder"); Copy leaves the base name whole as the remainder. It then
puts the taken text at the start or the end of the remainder, or at a 1-based position inside the remainder (position
p leaves p - 1 characters in front, as in Add, NS-4; a position past the end means the end). The separator goes between
the taken text and the remainder, only when the remainder is not empty. At a position inside the remainder (text
follows) it goes after the taken text; at the end of the remainder (position at or past its length) it goes before the
taken text, so a position past the end gives the same result as target End. Counts are UTF-16 characters; a cut never splits a surrogate pair
(the whole pair is taken, as in NS-20), and a position that falls inside a pair moves to just after it.
1. Given `2024report.txt`, Move, source Start, count 4, target End, separator `_`, then `report_2024.txt`.
2. Given `report_2024.txt`, Copy, source End, count 4, target Start, separator `-`, then `2024-report_2024.txt`.
3. Given `abcdef.txt`, Move, source Start, count 2, target Position 3, no separator, then `cdabef.txt` (remainder `cdef`,
   two characters stay in front). With separator `-`, then `cdab-ef.txt`. With position 5 (the end of the remainder) or
   99 and no separator, then `cdefab.txt`; with the separator `-`, then `cdef-ab.txt` for both (the same as target End).
   With position 1 and `-`, then `ab-cdef.txt` (the same as target Start). With position 4 and `-` (three characters stay in front), then `cdeab-f.txt`.
4. Given a count larger than the base name (`abc.txt`, Copy, source Start, count 10, target End, separator `-`), then the
   whole base name is taken: `abc-abc.txt`. Given the same with Move, then the remainder is empty and the name is
   unchanged, with no separator: `abc.txt`.
5. Given count 0 or negative, or target Position with a position that is null or below 1, then `Enabled` is false and
   the name is unchanged. Given `int.MaxValue` as the count or the position, then it is clamped with no error.
6. Given the taken text and the remainder on the same side (`abcd.txt`, Move, source End, count 2, target End, separator
   `_`), then `ab_cd.txt`: the rule always puts the separator between the two parts. With no separator, then the name is
   unchanged.
7. Given an emoji (a surrogate pair) followed by `a` (`😀a.txt`), Move, source Start, count 1, target End, then `a😀.txt`
   (the whole pair is taken). Given `a😀b.txt`, Move, source Start, count 1, target Position 2, then the remainder is `😀b`, the position falls inside the pair
   and moves after it: `😀ab.txt`.
8. Given a hidden dotfile `.gitignore`, Move, source Start, count 3, target End, separator `_`, then `tignore_.gi` (the
   whole name is the base, NS-1); given a folder `Folder 1.2`, Copy, source End, count 2, target Start, separator `_`,
   then `.2_Folder 1.2`.
9. Given a separator with a character Windows forbids (`:`), then it is applied as typed and the invalid-name check
   flags the result (pipeline test with `InvalidNameCheck`).
10. Given the pipeline with `RemovePositionRule(first: 1)` (stage 5), this rule (Move, source Start, count 2, target End,
    separator `_`, stage 6) and `AddRule(prefix: "P-")` (stage 7), on `xabcd.txt`, then `P-cd_ab.txt`, whatever order the
    rules were given.
11. Given a null separator, the constructor throws `ArgumentNullException`; given an undefined mode, source or target, it
    throws `ArgumentOutOfRangeException`.
12. Given the properties, then `Mode`, `Source`, `Count`, `Target`, `Position` and `Separator` return the values given.

## Out of scope
- The panel (NS-31).
- Counting words instead of characters; moving to a position counted from the end.
- Moving or copying files to another folder (NameShift only renames).

## Decisions
- Position counted how? **Decision:** from the start of the remainder, 1-based, as in Add and Numbering, so the same
  number means the same place everywhere (po, 2026-10-24).
- Where does the separator go? **Decision:** between the taken text and the remainder, only when both are not empty, so a
  separator never dangles at an edge. It sits on the side of the taken text that touches the remainder, so a position at
  the end behaves exactly like target End (`cdef-ab`) and position 1 like target Start (po, 2026-10-24, revised after the
  PR #39 review).
- Move to the same side? **Decision:** allowed and still adds the separator (criterion 6), so the rule has no special case
  (po, 2026-10-24).
- Count larger than the name? **Decision:** the whole base name is taken, as the first version of this story said (po,
  2026-10-24).
- A cut inside a surrogate pair? **Decision:** take the whole pair, because an unpaired surrogate makes an invalid name,
  as in NS-20 (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: criteria made testable (enums, separator, positions, pairs, order), set Ready (po).
