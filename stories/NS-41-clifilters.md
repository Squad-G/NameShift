# NS-41: Command line: recursion and filters

Status: Draft
Depends on: NS-38, NS-32, NS-33, NS-74
Area: app

## Why
Select files from a script like in the Filters panel.

## Acceptance criteria
1. Given `--recurse`, then files of sub-folders are included and renamed in place; reparse points are not followed.
2. Given `--mask "*.jpg;*.png"`, `--regex`, `--files-only`, `--folders-only`, `--include-hidden`, `--min-length` and `--max-length`, then each behaves like its filter in the window.
3. Given an invalid regex, then exit code 1.
4. Given filters that match nothing, then it prints `no files` and exits 0.

5. Given a mask in the path argument (`apply D:\Pics\*.jpg`), then only matching files of that folder are used, with the
   same mask code as `--mask` (NS-32).
6. Given NS-38 renames files only by default, then `--folders-only` renames only folders and `--include-folders` files and
   folders (the kinds filter of NS-32); `--files-only` stays the default and is accepted.

## Out of scope
- Anything not listed above.

## Decisions
- None needed.

## Log
- 2026-10-18: created (po).
