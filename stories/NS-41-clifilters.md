# NS-41: Command line: recursion and filters

Status: Draft
Depends on: NS-38, NS-32
Area: app

## Why
Select files from a script like in the Filters panel.

## Acceptance criteria
1. Given `--recurse`, then files of sub-folders are included and renamed in place; reparse points are not followed.
2. Given `--mask "*.jpg;*.png"`, `--regex`, `--files-only`, `--folders-only`, `--include-hidden`, `--min-length` and `--max-length`, then each behaves like its filter in the window.
3. Given an invalid regex, then exit code 1.
4. Given filters that match nothing, then it prints `no files` and exits 0.

## Out of scope
- Anything not listed above.

## Decisions
- None needed.

## Log
- 2026-10-18: created (po).
