# NS-24: RegEx rule

Status: Draft
Depends on: NS-1
Area: engine

## Why
Powerful match and replace for advanced users.

## Acceptance criteria
1. Given `IMG_(\d+)` replaced with `Photo $1` on `IMG_042.jpg`, then `Photo 042.jpg`.
2. Given an invalid pattern, then the rule reports an error for the panel (not per file) and has no effect.
3. Given the option include extension, then the pattern runs on the full name.
4. Given case-insensitive on, then `img` matches `IMG`.
5. Given a pattern that takes too long (catastrophic), then it is stopped after 100 ms per name and flagged as an error for that item.
6. Given an empty pattern, then the rule does nothing.

## Out of scope
- Anything not listed above.

## Decisions
- Which regex flavour? **Decision:** the .NET one, because it is built in and the panel says so (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
