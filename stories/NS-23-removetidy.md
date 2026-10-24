# NS-23: Remove: trim, leading dots, double spaces

Status: In Review
Depends on: NS-1
Area: engine

## Why
Tidy messy names: stray spaces and dots.

## Acceptance criteria
`RemoveTidyRule(bool trim = false, bool leadingDots = false, bool doubleSpaces = false)`, stage Remove (5), base name
only (the extension is never touched). `Enabled` is true when at least one option is on. `Apply` never throws. The
steps run in this order on the base name: double spaces, trim, leading dots, then trim again (only when trim is on).
1. Given `  a b .txt` (leading and trailing spaces in the base name) and trim, then `a b.txt`. Trim removes every
   leading and trailing character `char.IsWhiteSpace` accepts, from the base name only.
2. Given `..a.txt` and leading dots, then `a.txt`; given the dotfile `.gitignore`, then `gitignore` only when this
   option is on, and unchanged when it is off (the whole name is the base, NS-1). Given a folder `.hidden`, then
   `hidden`.
3. Given `a   b.txt` and double spaces, then `a b.txt`: every run of two or more spaces (U+0020) collapses to one
   space. A single space is unchanged, and other white space (a tab, a no-break space) is not touched.
4. Given `.. a.txt` with leading dots and trim, then `a.txt` (the space left behind is trimmed); given ` ..a.txt` with
   both, then `a.txt` (trim first, then the dots). Given the same names with leading dots alone, then ` a.txt` and
   ` ..a.txt` (the dots are removed only at the very start).
5. Given double spaces and trim together on `  a   b  .txt`, then `a b.txt`.
6. Given nothing to tidy (`a b.txt` with all three options on), then the name is unchanged and the result is
   `Unchanged`; given no option on, `Enabled` is false.
7. Given a file named only with dots and spaces in its base (`  .txt` with trim), then the base is empty and the
   result is `.txt` (valid per NS-5, not flagged); given a folder `   ` with trim, then the name is empty and the
   invalid-name check flags `EmptyName` (pipeline test with `InvalidNameCheck`).
8. Given `...` as a file name (no usable extension, so the whole name is the base) with leading dots, then the name is
   empty and is flagged `EmptyName`.
9. Given the extension, then it is never touched: `a  b.my  ext` is split at the last dot into base `a  b` and
   extension `my  ext`; double spaces gives `a b.my  ext`.
10. Given a name whose base starts with a dot and which has an extension (`..txt`, base `.`, extension `txt`), then
    leading dots gives `.txt` (a valid name per NS-5).
11. Given the same item run through a Replace rule (stage 3) then this rule (stage 5), then Replace runs first.

## Out of scope
- The panel (NS-30).
- Trailing dots (the base never ends in a dot before its extension; a final dot is the checker's job, NS-5).
- Changing other white space (tabs, no-break spaces) into spaces.

## Decisions
- Order of the steps? **Decision:** double spaces, trim, leading dots, trim again, so one click reaches a clean name
  whatever the mix of dots and spaces at the start (po, 2026-10-23).
- Is the dotfile an exception? **Decision:** no; with the option on, `.gitignore` loses its dot, because the user asked
  for it; the option is off by default (po, 2026-10-18).
- Which characters count as spaces? **Decision:** trim uses `char.IsWhiteSpace`; double spaces uses only U+0020, so
  rarer characters are never rewritten by surprise (po, 2026-10-23).
- Empty result on a file? **Decision:** `.txt` is valid (NS-5); a folder or an all-dots name is flagged `EmptyName`
  (po, 2026-10-23).

## Log
- 2026-10-18: created (po).
- 2026-10-23: criteria made testable (order, definitions, dotfiles, empty results), set Ready (po).
- 2026-10-23: In Progress (builder).
- 2026-10-23: In Review (builder), PR #36.
