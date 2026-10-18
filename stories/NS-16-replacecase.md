# NS-16: Replace rule: keep the case pattern of the found text

Status: Draft
Depends on: NS-2, NS-12
Area: engine

## Why
Optionally make the replacement follow the capitalisation of what it replaces.

## Acceptance criteria
1. Given the option off (default), then behaviour is unchanged from the Replace rule.
2. Given the option on, find `dump` with `bob`, then `Dump` becomes `Bob`, `DUMP` becomes `BOB`, `dump` stays `bob`.
3. Given a found text with mixed case (`dUmP`), then the replacement is used as typed.
4. Given an empty With, then nothing changes with the option on.
5. Given the Replace panel (NS-12), then it gets a checkbox "Match the case of the found text" (off by default, cleared by the panel's reset), saved in presets (NS-36) and available on the command line as `--replace-keep-case` (NS-40).

## Out of scope
- Anything not listed above.

## Decisions
- Default? **Decision:** off, see the Replace rule (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
