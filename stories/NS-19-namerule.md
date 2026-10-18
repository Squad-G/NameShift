# NS-19: Name rule: keep, remove, fixed, reverse

Status: Draft
Depends on: NS-1
Area: engine

## Why
Replace the whole base name or reshape it.

## Acceptance criteria
1. Given mode fixed `holiday` on `IMG_1.jpg`, then `holiday.jpg`.
2. Given mode remove, then the base name is empty and flagged `EmptyName` unless a later rule adds text.
3. Given mode reverse on `abc123.txt`, then `321cba.txt`.
4. Given mode keep, then the name is unchanged.
5. Given reverse on text with accents or surrogate pairs, then characters are reversed as whole characters (text elements).

## Out of scope
- Anything not listed above.

## Decisions
- Is `keep` needed? **Decision:** yes as the default, so the panel has a neutral state (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
