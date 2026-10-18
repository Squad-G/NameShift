# NS-7: Path length check

Status: Done
Depends on: NS-1
Area: engine

## Why
Warn before a rename would produce a path Windows cannot handle.

## Acceptance criteria
1. Given a folder path of 200 characters and a new name of 60, then the full path is 261 and the item is flagged `PathTooLong` (limit 259 characters plus terminator, i.e. full path longer than 259 is flagged).
2. Given a full path of exactly 259 characters, then it is not flagged.
3. Given the path already exceeds the limit and the name is unchanged, then it is not flagged.
4. Given a new name shorter than the old one in an over-long folder, then it is still flagged only if the resulting full path exceeds the limit.

## Out of scope
- Long-path (`\\?\`) support (can be a later story).

## Decisions
- Limit value? **Decision:** 259 characters, the classic limit, because it works in every Windows program including Explorer (po, 2026-10-18).
- Full path = `FolderPath` + `\` (not added when `FolderPath` already ends with `\` or `/`, e.g. a drive root `C:\`) + `NewName`, counted in UTF-16 units; flagged when over 259. Computed arithmetically, never with `Path.Combine` (it throws on .NET Framework for characters NS-5 flags) (tech-lead, 2026-10-18).
- Only changed results are checked, as in NS-5/NS-6; detail is null (tech-lead, 2026-10-18).
- A folder being renamed lengthens its children's paths; this check does not look at children (the story's criteria are about the item's own path only). A later story can add it if wanted. **Decision (David, 2026-10-18):** check only the renamed item's own path for now.

## Log
- 2026-10-18: created (po).
- 2026-10-18: set Ready, first round of the minimal usable slice (po).
- 2026-10-18: In Progress, branch feature/NS-7-path-length (builder).
- 2026-10-18: In Review, PR #11 (builder).
- 2026-10-18: Done, reviewer APPROVE; nits folded (empty-folder test: an empty `FolderPath` counts one separator, as the decision above says; trailing newlines); merged into dev (tech-lead).
- 2026-10-18: David confirmed the children's-paths decision (tech-lead).
