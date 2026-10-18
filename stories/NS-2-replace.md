# NS-2: Replace rule

Status: In Progress
Depends on: NS-1
Area: engine

## Why
Find text in names and replace it, the most used rename operation.

## Acceptance criteria
1. Given `Holiday 01.jpg`, find ` ` with `_`, then `Holiday_01.jpg`.
2. Given `Img.JPG`, find `img` with `pic`, match case off, then `pic.JPG`; with match case on, the name is unchanged.
3. Given `aaa.txt`, find `a` with `bb`, then `bbbbbb.txt` (all occurrences, left to right, no re-scan of replaced text).
4. Given an empty Find, then the rule does nothing.
5. Given an empty With, then the found text is removed: `a-b.txt`, find `-`, gives `ab.txt`.
6. Given scope = name only (default), when the name is `x.txt` and find is `txt`, then it is unchanged; with scope = name and extension it becomes the replacement.
7. Given find text containing regex characters such as `.` or `(`, then it is matched literally.
8. Given `DumpStack.log`, find `dump` with `bob`, match case off, then `bobStack.log` (the replacement is inserted exactly as typed; the original capitalisation is not carried over) and `.log` is untouched.

## Out of scope
- Regular expressions (NS-24).

## Decisions
- Match case default? **Decision:** off, because users rarely mean to distinguish case when replacing (po, 2026-10-18).
- Should a case-insensitive match copy the case of the found text (`Dump` -> `Bob`)? **Decision:** no by default, the replacement is always inserted exactly as typed, because it is predictable and matches the live preview; an opt-in option comes in a separate story (replacecase), because it is a clever extra not every user wants (po, 2026-10-18).
- Does it touch the extension? **Decision:** not by default; an option includes it, because changing extensions by accident is the costly mistake (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress, branch feature/NS-2-replace (builder).
