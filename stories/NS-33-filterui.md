# NS-33: Filters: settings model and filtered file list

Status: In Progress
Depends on: NS-32, NS-51, NS-66, NS-73
Area: app

## Why
Filter the list, in the window's logic (`NameShift.Ui`): a settings model and a file list that applies the filter. The
controls are NS-70, the sub-folder listing is NS-74, the numbering check box is NS-71. (Part a of the original NS-33.)

## Acceptance criteria
Names are indicative. All code is in `NameShift.Ui` (no WinForms); tests on net10.0 and net48; every new visible text is in
`Strings.resx`.

Filter settings (`FilterPanelModel`, plain properties, `Changed`, `Reset()`, `IsDefault`, as the rule panels' models):
1. Given a new model, then Mask and Pattern are empty, IgnoreCase false, IncludeFiles true, IncludeFolders true,
   IncludeHidden false, the MinLength and MaxLength texts empty, `IsDefault` is true, and `Filter` is an `ItemFilter` equal in
   behaviour to `new ItemFilter()`. (The sub-folders property is added by NS-74.)
2. Given any property is set to a different value, then `Changed` is raised once and `IsDefault` is false; set to the same
   value, nothing is raised; a null text counts as empty; `Reset()` restores every default and raises `Changed` once (not at
   all when already default).
3. Given MinLength text `5` and MaxLength text `8`, then `Filter.MinLength` is 5 and `Filter.MaxLength` is 8. Given text that
   is not a whole number from 0 up (`abc`, `-1`, `1.5`, `99999999999`), then that limit is ignored and `Error` says `Enter a
   whole number for the length.` (new string); the text is kept as typed.
4. Given an invalid regex pattern or a minimum above the maximum, then `Error` is the filter's own text (NS-32:7, 12); the
   model adds nothing of its own. With no problem `Error` is null. Several problems are joined by one space.
5. Given the filter changes, then `Filter` is a new, already built `ItemFilter` (patterns are not rebuilt per item).

File list (`FileListModel`):
6. Given a loaded folder, then `AllItems` holds every entry the listing returned (hidden and system ones too) in list order
   and `Items` holds `Filter.Apply(AllItems)`: with the default filter, `Items` equals what the window shows today (no hidden
   or system item). `DiskFileListSource.IsShown` is removed: the source returns hidden and system entries with their
   attributes (`ListEntries` keeps its signature), and the existing tests are updated.
7. Given a filter change, then `Items` is a new list, `Batch` is rebuilt, the selected items that are still listed stay
   selected (by item, as a sort does, NS-66), the source is not called again, and the single event of a new order
   (`OrderChanged`, NS-66, with the same consumers: `PreviewModel` recomputes once and `MainActions` clears the Rename lock)
   is raised once.
8. Given the filter hides every item of a non-empty folder, then `Message` is `No item matches the filters.` (new string)
   when the filter is not at default, and `This folder is empty` when it is at default (a folder that holds only hidden
   files reads as empty, as today). A folder with no entry at all says `This folder is empty`.
9. Given `ExistingNamesByFolder` (replaces `ExistingNames`: a map from folder path to every entry name of that folder, hidden
   and system included, never filtered; the type `RenamePlanner` takes, NS-73), then `PreviewModel` passes it to the
   planner: a file hidden by the filter still occupies its name (`a.png` filtered out, `a.jpg` renamed to `a.png` is
   flagged `TargetExists`, NS-32:15). Callers that change: `PreviewModel`, `FileListModel` and their tests; and the command
   line (NS-38) when it is merged, which switches to the same map and keeps files only with the default filter.
10. Given a 10,000-item list and a mask change, then re-filtering finishes in under 1 second (a loose guard, as NS-58).

## Out of scope
- The controls (NS-70), sub-folders (NS-74), the numbering check box (NS-71, engine NS-60).
- Saving filters in presets, a "not" mask, size or date filters (NS-32).

## Decisions
- Hidden files? **Decision:** the listing returns hidden and system entries too (with their attributes) and `ItemFilter`
  (NS-32) leaves them out by default, so "Hidden" is one switch in the filter, not a second listing; `ExistingNames` keeps
  every entry, now per folder. Renaming hidden or system files is the user's explicit choice (tick Hidden, NS-70) (po,
  2026-10-24).
- Where does the filter control sit? **Decided by David (2026-10-24):** one compact row above the file list (NS-70). The
  original NS-33 is split: this model, the listing (NS-74), the controls (NS-70), the check box (NS-71) (po, 2026-10-24).
- Empty message with a default filter? **Decision:** unchanged ("This folder is empty") so nothing changes for users who
  never touch the filter (po, 2026-10-24, from the PR #53 review).

## Log
- 2026-10-18: created (po).
- 2026-10-24: placement answered by David; split (po).
- 2026-10-24: after review: narrowed to the model and the filtered list; sub-folder listing moved to NS-74; empty-message
  rule, callers named, depends on NS-73 (po).
- 2026-10-24: In Progress (builder).
