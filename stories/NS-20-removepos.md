# NS-20: Remove: first n, last n, from-to

Status: Done
Depends on: NS-1
Area: engine

## Why
Cut characters by position.

## Acceptance criteria
`RemovePositionRule(int? first = null, int? last = null, int? from = null, int? to = null)`, stage Remove (5), base
name only (the extension is never touched). The three parts run in this order on the result of the previous one: first
n, last n, then from-to. Counts are UTF-16 characters, as in Add.
1. Given `IMG_0001.jpg`, first 4, then `0001.jpg`.
2. Given `file_copy.txt`, last 5, then `file.txt`.
3. Given `abcdef.txt`, from 2 to 4 (1-based, inclusive), then `aef.txt`.
4. Given n larger than the base name length (first 10 on `abc.txt`), then the base name becomes empty and the
   extension stays: `.txt`, a valid name (NS-5) that is not flagged. Given the same on a folder `abc`, the name is
   empty and the invalid-name check flags `EmptyName`.
5. Given from < 1, from > to, or either of from and to missing, then the from-to part is ignored.
6. Given n = 0, negative or null, then that part removes nothing; `Enabled` is false when no part applies.
7. Given to larger than the length (`abcdef.txt`, from 4 to 99), then it is clamped: `abc.txt`; given from past the end
   (from 9 to 12), nothing is removed.
8. Given first 2, last 2 and from 1 to 1 on `abcdefgh.txt`, then first gives `cdefgh`, last gives `cdef`, from-to gives
   `def.txt` (order and chaining).
9. Given a cut that would split a surrogate pair (an emoji + `a`, first 1), then the whole pair is removed (`a`): the
   result never holds half a character.
10. Given a hidden dotfile `.gitignore`, first 1, then `gitignore` (the whole name is the base, NS-1); given a folder
    `Folder 1.2`, last 2, then `Folder 1` (the whole name is the base).

## Out of scope
- The Remove panel (NS-30).
- Removing text, character classes, tidy (NS-21 to NS-23).

## Decisions
- Positions are 1-based and inclusive, as in Add (po, 2026-10-18).
- One class for three parts? **Decision:** yes, one rule with three optional parts, as Add does; the panel (NS-30)
  builds one rule per group (po, 2026-10-23).
- Split surrogate pair? **Decision:** remove the whole pair, since an unpaired surrogate makes an invalid name (po,
  2026-10-23).

## Log
- 2026-10-18: created (po).
- 2026-10-23: criteria made testable, order and surrogate handling decided, set Ready (po).
- 2026-10-23: In Progress (builder).
- 2026-10-23: In Review (builder), PR #32.
- 2026-10-24: criterion 10 example corrected: `Folder 1.2` minus its last 2 characters is `Folder 1`, not `Folder 1.` (tech-lead, from the PR #32 review; code and test already right).
- 2026-10-24: Done (tech-lead), PR #32 merged into dev.
