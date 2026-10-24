# NS-47: Open a folder from the command line or by drag and drop

Status: Ready
Depends on: NS-10, NS-51
Area: app

## Why
Start NameShift on a folder quickly: from a shortcut, a terminal, or by dropping a folder on the window.

## Acceptance criteria
The logic is in two small `NameShift.Ui` pieces with tests: `OpenTarget` (a static class in `NameShift.Ui`) and
`FileListModel.FindRow`. The window code only calls them.

`OpenTarget.FirstPathArgument(string[] args)` gives the text to open, or null:
1. Given `[]`, then null. Given `["D:\Pics"]`, then `D:\Pics`. Given `["D:\Pics", "E:\Other"]`, then `D:\Pics` (only the
   first, see Decisions). Given `["--verbose", "D:\Pics"]`, then `D:\Pics`: an argument that starts with `-`
   is an option, not a path, and is skipped. Given `[""]` or `["   "]`, then null.

`OpenTarget.Resolve(string? text, string currentDirectory, IFolderSource source)` returns an `OpenRequest` with `Folder`
(string?), `FileName` (string?) and `Message` (string?). It may block on the disk (the window calls it off the UI thread).
Tests use `FakeFolderSource` (its `Folders` and `Files`):
2. Given null, an empty text or white space, then `Folder`, `FileName` and `Message` are all null (the app opens normally,
   silently).
3. Given `D:\Pics` that is a folder, then `Folder` is `D:\Pics`, `FileName` null, `Message` null. Given the same with spaces
   (`D:\My Pics`), a trailing backslash (`D:\Pics\`), forward slashes (`D:/Pics`), surrounding quotes (`"D:\Pics"`) or surrounding
   spaces, then the same folder, as the path bar reads them (`PathInput.TryNormalize`). Given `D:` or `D:\`, then `D:\`.
4. Given the argument text `D:\My Pics"` or `D:\My Pics" --verbose`, as Windows delivers `"D:\My Pics\"` and
   `"D:\My Pics\" --verbose` (the backslash escapes the closing quote), then everything from the first `"` on is dropped, and
   the folder is `D:\My Pics`. Given `"D:\Pics"` (quotes kept around the text, as the path bar accepts) the quotes are removed
   first, so this rule only cuts a quote that is left over.
5. Given a path that is a file (`D:\Pics\a.jpg`), then `Folder` is `D:\Pics`, `FileName` is `a.jpg`, `Message` null.
6. Given a path that does not exist, then `Folder` and `FileName` are null and `Message` is `Folder not found: {0}` with the
   normalised full path (`Strings.PathNotFound`; `d:/pics/` shows `d:\pics`). Given a path with a character Windows forbids (`D:\Pics?`) or one
   that is not a path (`a|b`), then `Message` is `That is not a valid folder path.` (`Strings.PathInvalid`). Neither throws.
7. Given a relative path, then it is resolved against `currentDirectory` before the check: with `C:\Work`, `Pics` gives
   `C:\Work\Pics`, `.` gives `C:\Work`, `..\Pics` gives `C:\Pics`. Given a `currentDirectory` that is empty or not a full
   path, then a relative path gives the invalid message.
8. Given a path starting `\\?\`, then it is refused as invalid (the path bar refuses it too, NS-10). A network path
   (`\\server\share\Docs`) that is a folder opens like any other.

The model:
9. Given the loaded items `a.jpg`, `B.txt`, then `FileListModel.FindRow("b.txt")` is 1 (ordinal, ignoring case), `FindRow("c.txt")`
   is -1, and `FindRow` before a folder is loaded (or when it failed) is -1. A hidden file, which the list does not show, is
   -1 too.

In the window (by hand, listed in the PR):
10. `NameShift.exe "D:\Pics"` opens on that folder (tree, path bar and list), not on Documents; with a path that does not
    exist the window opens on Documents and the status bar shows the message; with no argument it opens as today. The start
    does not flash Documents first when a folder is given.
11. Given a file path (`NameShift.exe "D:\Pics\a.jpg"`), then the folder opens, the row `a.jpg` is selected and scrolled into
    view (and the `N of M files` count shows `1 of M`). Given the file is not listed (hidden, or removed), then the folder
    opens with nothing selected.
12. Dropping a folder on the window opens it; dropping a file opens its folder and selects the file, as above. Dropping
    several items uses only the first. A drop of something that is not a file or folder (text) shows the no-drop cursor. The
    drop works over the tree, the list, the path bar and the panels. Nothing happens, and the no-drop cursor shows, while a
    Rename or Revert runs (the window is locked). A dropped path that is not found shows the message in the status bar.
13. The same path again (dropping the folder already shown) changes nothing and shows no message; selecting the file in
    that folder still selects it.

## Out of scope
- The verbs of the command line (`apply`, `undo`, help: NS-38 and after): a bare path with no verb opens the window. Until
  NS-38 exists, `NameShift.exe apply D:\Pics` would read `apply` as a relative folder and show "Folder not found".
- Several windows or one window per path; reusing a running window (single instance).
- Opening more than one dropped folder; renaming from the command line (NS-38).
- The Explorer right-click entry (NS-49): it uses this story.

## Decisions
- Relation to the command-line stories (NS-38 to NS-43)? **Decision:** this story is only the window: `NameShift.exe <folder>`
  opens the window on a folder. Renaming from a console is the `apply`/`undo` verbs of NS-38 onward; a bare folder
  argument with no verb opens the window, so there is no overlap (po, 2026-10-18).
- Multiple dropped items? **Decision:** only the first, to stay simple (po, 2026-10-18).
- Relative path? **Decision:** resolved against the process's current directory, so `NameShift.exe .` works from a
  terminal and a shortcut's "Start in" works (po, 2026-10-24).
- The closing-quote trap? **Decision:** `"D:\Pics\"` reaches the program as `D:\Pics"`; the leftover quote and everything
  after it are dropped, instead of refusing a path every Explorer "Copy as path" user can produce (po, 2026-10-24).
- File given? **Decision:** open its folder and select it, because a right-click or a drop on a file means "this one"
  (po, 2026-10-24).
- Message for a bad path? **Decision:** the status bar, never a dialog, and the window still opens normally (CLAUDE.md:
  routine feedback goes to the status bar) (po, 2026-10-24).
- Where does the start resolve? **Decision:** before the first folder is shown, off the UI thread (one disk check), then
  one navigation, so Documents is never loaded first for nothing (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: depends on NS-51 too (file selection); criteria made testable (`OpenTarget`, `FindRow`) and the window checks listed; set Ready (po).
