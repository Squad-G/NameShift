# NS-21: Remove: characters, text, crop before/after

Status: Done
Depends on: NS-1
Area: engine

## Why
Cut named characters, a piece of text, or everything before or after a marker.

## Acceptance criteria
`RemoveTextRule(string characters = "", string text = "", bool wholeWord = false, bool matchCase = false,
string cropBefore = "", string cropAfter = "", bool keepMarker = false)`, stage Remove (5), base name only (the
extension is never touched). Text is matched ordinally (never culture-dependent), as in Replace. The parts run in this
order, each on the result of the previous one: crop before, crop after, text, characters. The constructor throws
`ArgumentNullException` for a null string (as `ReplaceRule`) and never for odd values; `Apply` never throws.
1. Given `a-b_c.txt` and characters `-_`, then `abc.txt`. Every character of the list is removed wherever it occurs; a
   space in the list removes spaces; the list needs no separators.
2. Given `my old file.txt` and text `old` (match case off), then `my  file.txt` (two spaces: tidy is NS-23's job); given
   `my OLD file.txt` and text `old`, then `my  file.txt`; with match case on, `my OLD file.txt` is unchanged.
3. Given text removal, then every occurrence is removed left to right without overlap: `aaa.txt` with text `aa` gives
   `a.txt`.
4. Given whole word on and text `cat`, then `concatenate.txt` is unchanged, `my cat.txt` becomes `my .txt`, and
   `cat_dog.txt` becomes `_dog.txt`. A match counts only when the character before it and the one after it are not
   letters or digits (`char.IsLetterOrDigit`), the start and end of the base name counting as boundaries; `_`, `-`,
   `.`, and spaces are boundaries.
5. Given `Report - Final.txt` and cropBefore ` - `, then `Final.txt`; given cropAfter ` - `, then `Report.txt`. The
   marker is removed too by default. With keepMarker on, cropBefore gives ` - Final.txt` and cropAfter gives
   `Report - .txt`.
6. Given a marker that occurs more than once (`a - b - c.txt`), then both crops use the first occurrence from the
   left: cropBefore ` - ` gives `b - c.txt`, cropAfter ` - ` gives `a.txt`. Given both markers (before `[`, after `]`)
   on `x[keep]y.txt`, then `keep.txt`: crop after runs on the result of crop before.
7. Given a marker that is not found, an empty marker, an empty characters list or an empty text, then that part does
   nothing. `Enabled` is false when all four of characters, text, cropBefore and cropAfter are empty.
8. Given matchCase off (default), then markers, text and characters all match ignoring case (`IMG-1.jpg` with
   characters `i` gives `MG-1.jpg`); with matchCase on, only the exact case matches.
9. Given the extension, then it is never read or changed: `a.txt.bak` with text `txt` removes `txt` from the base
   `a.txt` only, giving `a..bak`; given characters `t` on `test.txt`, then `es.txt`.
10. Given a result that is empty, then a file keeps its extension (`-_.txt` with characters `-_` gives `.txt`, a valid
    name per NS-5, not flagged), and a folder `---` with characters `-` gives an empty name flagged `EmptyName` by the
    existing invalid-name check (pipeline test with `InvalidNameCheck`).
11. Given a character outside the BMP in the characters list (an emoji, two UTF-16 units) and a name that contains
    it, then the whole character is removed; given a name that contains only half-matching units (another emoji
    sharing the high surrogate), then it is not touched. The result never holds a lone surrogate.
12. Given a hidden dotfile `.gitignore` and text `git`, then `.ignore` (the whole name is the base, NS-1); given a
    folder `Folder 1.2` and cropBefore `1`, then `.2`.
13. Given the same name through the pipeline with a Replace rule (stage 3) and this rule (stage 5), then Replace runs
    first (stage order).

## Out of scope
- The panel (NS-30).
- A list of words (one text field is enough), regular expressions (NS-24), crop at the last occurrence.
- Tidying leftover spaces (NS-23).

## Decisions
- Is the old "remove word" the text option? **Decision:** yes, one `text` field, with a whole-word option for the
  "word" case; a separate list of words adds fields users did not ask for (po, 2026-10-23).
- Does crop keep the marker? **Decision:** default removes it, since most users want it gone; option `keepMarker`
  keeps it, for both crops (po, 2026-10-18).
- First or last occurrence for crops? **Decision:** the first from the left, for both; simple and predictable
  (po, 2026-10-23).
- Match case default? **Decision:** off, like Replace; it applies to characters, text and markers (po, 2026-10-23).
- Order of parts? **Decision:** crops first (they find their marker in the original name), then text, then
  characters (po, 2026-10-23).
- Underscore as a word boundary? **Decision:** yes, only letters and digits are word characters; file names use `_` as
  a separator (po, 2026-10-23).

## Log
- 2026-10-18: created (po).
- 2026-10-23: criteria made testable (order, case, whole word, markers, surrogates, empty results), set Ready (po).
- 2026-10-23: In Progress (builder).
- 2026-10-23: In Review (builder), PR #34.
- 2026-10-24: Done (tech-lead), PR #34 merged into dev.
