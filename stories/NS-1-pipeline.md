# NS-1: Rename pipeline and rule contract

Status: Ready
Depends on: 
Area: engine

## Why
A single engine that takes a list of files and a stack of rules and returns the new name of every file, without touching the disk.

## Acceptance criteria
1. Given files `a.txt`, `b.txt` and no rules, when the pipeline runs, then each new name equals its old name and the item is marked unchanged.
2. Given a name is the pair (base name, extension) split at the last dot, then `a.tar.gz` is (`a.tar`, `gz`), `.gitignore` is (`.gitignore`, empty) and `Makefile` is (`Makefile`, empty).
3. Given two rules R1 then R2 where R1 appends `_x` to the base name and R2 upper-cases it, when run on `ab.txt`, then the result is `AB_X.txt` (rules apply in the fixed pipeline order, each seeing the previous output).
4. Given a rule is disabled, when the pipeline runs, then that rule has no effect.
5. Given the item list is `[b, a]`, when the pipeline runs, then results come back in the same order as the input and rules receive the item index (needed by numbering).
6. Given a rule throws, when the pipeline runs, then the item gets an error status naming the rule and the other items are still computed.
7. The pipeline never reads or writes the disk; it works on names, sizes and dates supplied in the input (tests use plain objects).

## Out of scope
- Any concrete rule (separate stories).
- Conflict checks (NS-5 to NS-7).

## Decisions
- Is a hidden dotfile like `.gitignore` a name with an empty base? **Decision:** the whole name is the base name and the extension is empty, because a rename of `.gitignore` should not produce a nameless file (po, 2026-10-18).
- Rule order configurable? **Decision:** no, fixed as in docs/PLAN.md, because it keeps behaviour predictable and the window simple (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
