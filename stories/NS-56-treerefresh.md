# NS-56: Folder tree follows Rename and Revert

Status: Ready
Depends on: NS-10, NS-15
Area: app

## Why
After Rename or Revert renames sub-folders, the tree keeps their old names until they are expanded again, and clicking
one says "Folder not found". The tree must show what is on disk once the action has finished.

## Background (current code)
- `FolderTreeModel` lists a node's children once (`ExpandAsync` returns at once when the node is `Loaded`) and never
  lists again. The tree control renders from that and only re-renders a node that is not on screen.
- `MainActions.RunAsync` reloads the file list of the shown folder after a Rename or Revert, but nothing tells the tree.
- Rename and Revert only change entries directly inside the shown folder (`_files.FolderPath`), so only that tree
  node's children can be stale.

## Acceptance criteria
In `NameShift.Ui` (tests in `NameShift.Ui.Tests`, with a fake `IFolderSource`):
1. Given a `Loaded` node `C:\Photos` with children `Old` and `Keep`, and the source now lists `New` and `Keep`, when
   `RefreshAsync(@"C:\Photos")` completes, then its children are `Keep`, `New` (source order) and no node named `Old`
   remains.
2. Given the same refresh, then the `Keep` node is the same `FolderNode` object as before (so its `State`, its loaded
   children and the view's expanded state are kept), and `New` is `NotLoaded`.
3. Given a refresh where a child that was `Loaded` with its own children is renamed (listed under a new name), then the
   new node is `NotLoaded` (its content is listed again when expanded); no stale grandchildren survive.
4. Name matching is case-insensitive: given `Photos` listed as `photos` after a case-only rename, then the node is
   replaced by one named `photos` (the displayed name is the disk name) and its `FullPath` uses the new spelling.
5. Given a node that is `NotLoaded` (never expanded), when `RefreshAsync` is called for it, then nothing is listed and
   its state is unchanged (the first expand lists it fresh).
6. Given a path the tree does not hold (outside the roots, or below a node that is `NotLoaded`), then `RefreshAsync`
   completes without error and changes nothing.
7. Given the source reports a problem for the node (for example `NotFound` because the folder itself was renamed or
   deleted), then the node is refreshed from its parent instead: the parent is re-listed, and so on up to the
   nearest folder that exists. `RefreshAsync` returns the path of the deepest node that still exists, so the caller can
   select it. Given the folder lists fine, it returns the same path.
8. Given a refresh that ends with no sub-folder, then `MayHaveChildren` is false (no expand arrow); given one that
   ends with at least one, it is true.
9. Given a refresh already running for a node, a second call for it returns the same task, and a node being expanded
   (`Loading`) is waited for first. The listing runs off the calling thread (`Task.Run`) like `ExpandAsync`; the
   source throwing never escapes (the node gets `Failed` / `Unavailable`, as in `ExpandAsync`).
10. `MainActions` raises a `FolderContentsChanged` event (arguments: the folder path the action ran on) on the calling
    thread after the file list reloaded, after a Rename or Revert that ran, even when the report says some items were
    skipped or failed. It is not raised when the action did not run (not allowed, or the user said No to the
    confirmation). It fires before `IsBusy` goes back to false.

In the window (`MainForm` and `FolderTree`; checked by hand and listed in the PR):
11. `MainForm` handles that event by calling `RefreshAsync` for the path and re-rendering that tree node from the model.
    Re-render keeps the tree nodes whose `FolderNode` object survived (same expanded or collapsed state, same
    children), adds the new ones, removes the gone ones. The tree does not collapse, flicker back to "Loading..." for
    the untouched nodes, or scroll away from the selection.
12. Given folder `A` shown and selected, Rename turns its sub-folder `x` into `y`, then the tree shows `y` (not `x`)
    under `A` with `A` still expanded and selected, and clicking `y` opens it with no "Folder not found". Revert
    brings `x` back the same way.
13. Selected folder renamed: the shown folder is the selected one and Rename and Revert only touch what is inside it,
    so this cannot come from these buttons. If the refresh finds the shown folder gone anyway (criterion 7), the tree
    selects the path `RefreshAsync` returned and the window navigates there (`FolderNavigator.SelectFromTree`); the
    status bar says the folder is no longer there (new string in `Strings.resx`).
14. Selecting `y` right after the refresh still goes through the normal navigation; no path is typed or reloaded twice.

## Out of scope
- Watching the disk for changes made outside NameShift (a refresh button or a file watcher is a separate story).
- Refreshing folders other than the one the action ran on (no recursion exists yet; revisit with NS-33).
- Renaming the shown folder itself (not possible in the app today).

## Decisions
- Re-list the node or rebuild the tree? **Decision:** re-list only the shown folder's node and reuse unchanged
  `FolderNode` objects, so expanded state and selection survive; a full rebuild would collapse the user's tree.
- Where does the trigger live? **Decision:** an event on `MainActions`, handled in `MainForm`; the tree and the actions
  stay unaware of each other, and the trigger is testable without WinForms.
- A renamed node that had loaded children? **Decision:** it becomes a fresh `NotLoaded` node; guessing that it is the
  same folder under a new name could show wrong content, and a re-list on expand is cheap.
- Selected folder gone? **Decision:** go to the nearest existing parent with a status message; never leave the window
  on a folder that does not exist.
- Refresh on every action or only when sub-folders changed? **Decision:** every time; one listing of one folder is
  cheap and the rule stays simple.

## Log
- 2026-10-23: created and set Ready (po). Bug found in the shipped slice (NS-10, NS-15).
