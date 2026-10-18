# NS-21: Remove: characters, words, crop before/after

Status: Draft
Depends on: NS-1
Area: engine

## Why
Cut named characters or text around a marker.

## Acceptance criteria
1. Given `a-b_c.txt`, remove characters `-_`, then `abc.txt`.
2. Given `my old file.txt`, remove word `old`, then `my  file.txt` (only the word; double spaces are the tidy story's job) with match case off.
3. Given `Report - Final.txt`, crop before ` - `, then `Final.txt`; crop after gives `Report.txt` (the marker is removed too by default).
4. Given the marker is not found, then the name is unchanged.
5. Given a whole-word option, then `cat` is not removed from `concatenate`.
6. Given an empty character list or marker, then nothing happens.

## Out of scope
- Anything not listed above.

## Decisions
- Does crop keep the marker? **Decision:** option, default removes it, since most users want it gone (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
