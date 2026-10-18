# NS-5: Invalid Windows names check

Status: In Progress
Depends on: NS-1
Area: engine

## Why
Never let a name through that Windows will refuse or silently change.

## Acceptance criteria
1. Given a new name containing any of `\ / : * ? " < > |` or a control character, then the item is flagged `InvalidCharacter` and names the offending character.
2. Given a new name whose text before the FIRST dot (the whole name if there is no dot) is `CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9` or `LPT1`-`LPT9`, in any case, then it is flagged `ReservedName`: `nul.txt`, `CON.txt`, `nul.tar.gz`, `con.backup.txt` and `Com1` are flagged (Windows ignores everything after the first dot, so this does not use the extension split of NS-1).
3. Given a new name ending in a dot or a space (`file.`, `file `), then it is flagged `TrailingDotOrSpace`.
4. Given a new name that is empty or only dots or spaces, then it is flagged `EmptyName`.
5. Given `con-x.txt`, `COM10.txt` or `xcon.txt`, then it is not flagged.
6. Given an unchanged name that is already invalid (it exists on disk), then it is not flagged, because it will not be renamed.
7. Given a base name or full name over 255 characters, then it is flagged `NameTooLong`.

## Out of scope
- Auto-fixing invalid names (the user fixes the rules).

## Decisions
- Silently strip invalid characters? **Decision:** no, flag and block, because silent changes surprise people and break numbering (po, 2026-10-18).

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress, branch feature/NS-5-invalid-names (builder).
