# Stories

One file per story: `stories/NS-<number>-<slug>.md`. Numbers are never reused. The `po` agent writes them and keeps
`stories/BACKLOG.md` (the ordered list) up to date.

```markdown
# NS-12: Numbering rule

Status: Draft            # Draft | Ready | In Progress | In Review | Done | Cancelled
Depends on: NS-3
Area: engine             # engine | app | installer | docs

## Why
One or two sentences: what the user gets.

## Acceptance criteria
1. Given …, when …, then …   (each one testable)

## Out of scope
- …

## Decisions
- Question? **Decision:** the answer, and why (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
```

The `po` makes a story `Ready` once it has no open question. The PR that builds a story also updates its file
(status, log); the tech lead sets `Done` in the last commit before merging.
