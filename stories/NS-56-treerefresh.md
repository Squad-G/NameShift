# NS-56: Folder tree follows Rename and Revert

Status: In Progress
Depends on: NS-10, NS-15
Area: app

## Why
After Rename or Revert renames sub-folders, the tree keeps their old names until they are expanded again, and clicking
one says "Folder not found". The tree must show what is on disk once the action has finished.

## Background (current code)
- `FolderTreeModel` lists a node's children once (`ExpandAsync` returns at once when the node is `Loaded`) and never
  lists again. The tree control renders from that and only re-renders a node that is not on screen.
- `MainActions.RunAsync` reloads the file list of the shown folder after a Rename or Revert, but nothing tells the tree.
- Rename only changes entries directly inside the shown folder (`_files.FolderPath`). Revert undoes the journal's last
  batch, which can be in other folders than the shown one (rename in A, go to B, Revert: A changes). The folders a
  batch touched are `LastBatch.Folders` (read before the Revert runs: afterwards the journal reads Reverted and its
  list is empty). So the tree nodes to refresh are those folders, plus the shown folder.

## Acceptance criteria
In `NameShift.Ui` (tests in `NameShift.Ui.Tests`, with `FakeFolderSource`):
1. Given a `Loaded` node `C:\Photos` with children `Old` and `Keep`, and the source now lists `New` and `Keep`, when
   `RefreshAsync(@"C:\Photos")` completes, then its children are `Keep`, `New` (source order) and no node named `Old`
   remains.
2. Given the same refresh, then the `Keep` node is the same `FolderNode` object as before (so its `State`, its loaded
   children and the view's expanded state are kept), and `New` is `NotLoaded`.
3. Given a refresh where a child that was `Loaded` with its own children is renamed (listed under a new name), then the
   new node is `NotLoaded` (its content is listed again when expanded); no stale grandchildren survive.
4. Name matching is case-insensitive: given `Photos` listed as `photos` after a case-only rename, then the node is
   replaced by one named `photos` (the displayed name is the disk name) and its `FullPath` uses the new spelling.
5. Given a node that is `NotLoaded` (never expanded), then `RefreshAsync` lists nothing, changes nothing and returns
   null (the first expand lists it fresh).
6. Given a path the tree does not hold (outside the roots, or below a node that is `NotLoaded`), then `RefreshAsync`
   completes without error, changes nothing and returns null.
7. Return value: the path of the node that was refreshed when its folder is still there; null when nothing was
   refreshed (criteria 5 and 6, or the node is a root that reports a problem: then the root is marked `Failed` with
   that problem and there is no parent to fall back to).
8. Given the source reports any listing problem for a non-root node (`NotFound`, `AccessDenied`, `NotReady`,
   `Unavailable`), then the parent is re-listed (recursively, by this same rule, so a parent that has a problem
   climbs again). Then:
   - the parent still lists the folder: it is kept at its own path in state `Failed` with that problem, and
     `RefreshAsync` returns the folder's own path (an access-denied folder stays visible and selectable);
   - the parent no longer lists it (renamed, deleted): the node is removed, and `RefreshAsync` returns the path of
     the nearest ancestor that was refreshed.
9. Given a `Failed` node whose listing now works, then the refresh makes it `Loaded` with its children and clears its
   `Problem`.
10. Given a refresh that ends with no sub-folder, then `MayHaveChildren` is false (no expand arrow); given one that
    ends with at least one, it is true.
11. Given a refresh already running for a node, a second call for it returns the same task, and a node being expanded
    (`Loading`) is waited for first. The listing runs off the calling thread (`Task.Run`) like `ExpandAsync`; the
    source throwing never escapes (the node gets `Failed` / `Unavailable`, as in `ExpandAsync`).
12. `MainActions` raises a `FolderContentsChanged` event on the calling thread after the file list reloaded, after a
    Rename or Revert that ran, even when the report says some items were skipped or failed. It fires before `IsBusy`
    goes back to false and not when the action did not run (not allowed, or the user said No to the confirmation).
    Its arguments are the folders to refresh: for Rename, the shown folder the batch was made from; for Revert, the
    folders of the last batch as read before the Revert ran (`LastBatch.Folders`) plus the shown folder, without
    duplicates (case-insensitive). Test: Revert of a batch in folder A while B is shown gives A and B.
13. Given the event, a helper in `NameShift.Ui` (or `MainActions`) that refreshes each folder in turn through
    `FolderTreeModel.RefreshAsync` and returns the paths that were refreshed (null results dropped) is tested with the
    fakes: two folders give two refreshes, and one that fails does not stop the other.

In the window (`MainForm` and `FolderTree`; checked by hand and listed in the PR):
14. `MainForm` handles the event by calling the helper and re-rendering each refreshed tree node from the model.
    Re-render keeps the tree nodes whose `FolderNode` object survived (same expanded or collapsed state, same
    children), adds the new ones, removes the gone ones. The tree does not collapse, flicker back to "Loading..." for
    the untouched nodes, or scroll away from the selection.
15. Given folder `A` shown and selected, Rename turns its sub-folder `x` into `y`, then the tree shows `y` (not `x`)
    under `A` with `A` still expanded and selected, and clicking `y` opens it with no "Folder not found". Revert
    brings `x` back the same way. Given Revert of a batch in `A` while `B` is shown, `A`'s children in the tree are
    right too, and `B` stays shown.
16. Selected folder gone: Rename and Revert never rename the shown folder itself, but a Revert in another folder can
    undo the rename of a folder that is an ancestor of the shown one. If the shown folder is no longer on disk (its tree node was removed by the refresh, so
    `RefreshAsync` returned an ancestor's path instead of the shown folder's own), the tree selects the nearest refreshed ancestor and the window navigates there (`FolderNavigator.SelectFromTree`); the
    status bar says the folder is no longer there (new string in `Strings.resx`). Test of the navigator side:
    `SelectFromTree` with the path already current raises no `FolderChanged`, with a different path raises it once, so
    nothing is loaded twice.

## Out of scope
- Watching the disk for changes made outside NameShift (a refresh button or a file watcher is a separate story).
- Recursion into sub-folders (no recursive rename exists yet; revisit with NS-33).
- Renaming the shown folder itself (not possible in the app today).

## Decisions
- Build order? **Decision:** NS-56 before NS-57: both change `MainActions.RunAsync` and its tests (po, 2026-10-23).
- Re-list the node or rebuild the tree? **Decision:** re-list only the affected nodes and reuse unchanged
  `FolderNode` objects, so expanded state and selection survive; a full rebuild would collapse the user's tree.
- Which folders? **Decision:** for Rename the shown folder; for Revert the journal batch's folders plus the shown
  folder, because Revert can act on a folder that is not shown (tech lead, 2026-10-23).
- Listing problem on a folder (criterion 8)? **Decision:** any problem, access denied included, climbs to the parent.
  If the parent still lists the folder it stays, marked `Failed`, at its own path; only a folder the parent no longer
  lists is removed. Reason: a locked folder must not vanish from the tree (po, confirming the tech lead's reading).
- Where does the trigger live? **Decision:** an event on `MainActions`, handled in `MainForm`; the tree and the actions
  stay unaware of each other, and the trigger is testable without WinForms.
- A renamed node that had loaded children? **Decision:** it becomes a fresh `NotLoaded` node; guessing that it is the
  same folder under a new name could show wrong content, and a re-list on expand is cheap.
- Shown folder gone? **Decision:** go to the nearest existing ancestor with a status message; never leave the window
  on a folder that does not exist.
- Refresh on every action or only when sub-folders changed? **Decision:** every time; a few listings are cheap and the
  rule stays simple.

## Log
- 2026-10-23: created and set Ready (po). Bug found in the shipped slice (NS-10, NS-15).
- 2026-10-23: after review and tech lead notes: Revert targets the batch's folders, return value and problem handling pinned down, criterion 14 made testable (po).
- 2026-10-23: In Progress (builder).
