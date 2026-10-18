# NS-1: Rename pipeline and rule contract

Status: In Progress
Depends on:
Area: engine

## Why
A single engine that takes a list of files and a stack of rules and returns the new name of every file, without touching the disk. It also fixes the contract every rule story builds on.

## Acceptance criteria
1. Given files `a.txt`, `b.txt` and no rules, when the pipeline runs, then each new name equals its old name and the item is marked unchanged.
2. Given a file name is split into (base name, extension) at the last dot, then `a.tar.gz` is (`a.tar`, `gz`), `.gitignore` is (`.gitignore`, empty) and `Makefile` is (`Makefile`, empty). A folder has no extension: its whole name is the base name (`v1.2` is (`v1.2`, empty)).
3. The item given to the pipeline carries, as read-only facts: original full name, folder path, kind (file or folder), attributes (hidden, system, read-only), size, created and modified times, its index in the input list (0-based) and the list's item count. Tests build items from plain objects; the pipeline never reads or writes the disk.
4. A rule declares its stage, a number 1 to 11 matching the pipeline order in `docs/PLAN.md` (RegEx = 1 ... Extension = 11). Given rules registered in any order, then the pipeline runs them sorted by stage (stable for equal stages), and each rule receives the item's current (base name, extension) as left by the previous rule and returns the new (base name, extension).
5. Given two test rules, R1 at stage 7 appending `_x` to the base name and R2 at stage 4 upper-casing it, when run on `ab.txt`, then the result is `AB_x.txt` whichever order they were registered in (stage 4 runs first giving `AB`, then stage 7 appends `_x`).
6. Given a rule is disabled, when the pipeline runs, then that rule has no effect.
7. Given the item list is `[b, a]`, when the pipeline runs, then results come back in the same order as the input and each rule receives that item's index.
8. Given a rule throws, when the pipeline runs, then that item gets an error status naming the rule and the other items are still computed.

## Out of scope
- Any concrete rule (separate stories).
- Conflict checks (NS-5 to NS-7).

## Decisions
- Is a hidden dotfile like `.gitignore` a name with an empty base? **Decision:** the whole name is the base name and the extension is empty, because a rename of `.gitignore` should not produce a nameless file (po, 2026-10-18).
- Rule order configurable? **Decision:** no, fixed as in docs/PLAN.md, because it keeps behaviour predictable and the window simple (po, 2026-10-18).
- The existing skeleton (a rule interface taking only a name and an index, and a pipeline splitting names with `Path.GetFileNameWithoutExtension`, which gives an empty base for `.gitignore`) does not meet this contract. **Decision:** this story replaces or extends it, and `ReplaceRule` is reworked in NS-2 to the new contract, because the contract must be settled once here or every rule story reopens it (po, 2026-10-18).
- Who sets an item's index and count? **Decision:** the pipeline, from the item's position in the list it is given, passed to each rule with the item (`ItemContext`), so they can never disagree with the list (tech-lead, 2026-10-18).
- How is a name ending in a dot (`a.`) split? **Decision:** like a name without a dot (whole name is the base), so splitting and joining always gives back the same name (tech-lead, 2026-10-18).
- What happens to an item after a rule fails? **Decision:** it keeps its original name, later rules are skipped for it, and the error names the rule's stage (tech-lead, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: contract made precise after review: item fields, stage declaration, skeleton replaced (po).
- 2026-10-18: In Progress, branch feature/NS-1-pipeline (builder).
