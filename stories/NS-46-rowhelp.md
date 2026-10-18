# NS-46: Clear row messages and column sorting

Status: Draft
Depends on: NS-13
Area: app

## Why
Understand why a row has a problem and order the list how I want.

## Acceptance criteria
1. Given a conflict row, then its Status text says what to change in plain words, for example `Another file will get this name` or `Windows does not allow ?`.
2. Given I click a column header, then the list sorts by it (Name natural, Size, Modified) and the numbering order follows.
3. Given I click again, then the order reverses; the sort shows an arrow.
4. Given sorting, then the selection is kept.

## Out of scope
- Anything not listed above.

## Decisions
- Better than classic renamers that show only a red cell. **Decision:** every flag has a sentence (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
