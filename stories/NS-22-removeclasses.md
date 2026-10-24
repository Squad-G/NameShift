# NS-22: Remove: digits, symbols, accents, high characters

Status: Draft
Depends on: NS-1
Area: engine

## Why
Strip whole classes of characters.

## Acceptance criteria
1. Given `a1b2.txt`, remove digits, then `ab.txt`.
2. Given `a!b@c#.txt`, remove symbols (punctuation and symbols, not letters, digits or spaces), then `abc.txt`.
3. Given `café.txt`, remove accents, then `cafe.txt` (letters decompose, marks dropped; `ß` and `ø` stay).
4. Given `a€b.txt`, remove high characters (anything above U+007F), then `ab.txt`.
5. Given a folder `123` with remove digits (an empty name), then `EmptyName` is flagged by the invalid-names check;
   given the file `123.txt`, the result `.txt` is valid (NS-5) and not flagged.

## Out of scope
- Anything not listed above.

## Decisions
- Transliterate `ø` to `o`? **Decision:** no, only Unicode decomposition, because a hand-written table invites mistakes (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
