# NS-32: Filters: mask, regex, kinds and length

Status: Ready
Depends on: NS-1
Area: engine

## Why
Decide which files and folders take part, before any rule runs: only the `.jpg` files, only names that start with `IMG_`,
no folders. The window (NS-33) and the command line (NS-41) both use it.

## Acceptance criteria
A new public sealed class `ItemFilter` in `NameShift.Core`:
`ItemFilter(string mask = "", string pattern = "", bool ignoreCase = false, bool includeFiles = true, bool includeFolders =
true, bool includeHidden = false, int? minLength = null, int? maxLength = null)`. It is a plain yes/no on the item's own
facts, built once and read-only; it never touches the disk and never changes an item. Members: the eight values as
properties (`Mask`, `Pattern`, `IgnoreCase`, `IncludeFiles`, `IncludeFolders`, `IncludeHidden`, `MinLength`, `MaxLength`),
`string? Error`, `bool Includes(RenameItem item)` and `IReadOnlyList<RenameItem> Apply(IReadOnlyList<RenameItem> items)`
(the included items, same instances, same order). An item is included only when every condition below holds; an empty
condition holds for every item. `new ItemFilter()` therefore includes everything except hidden and system items.
A null `mask` or `pattern`, a null item or list, or a null item in the list throws `ArgumentNullException`; a negative
`minLength` or `maxLength` throws `ArgumentOutOfRangeException`. The constructor never throws for a bad pattern.

Mask (matched against the original full name, extension included; folders too):
1. Given mask `*.jpg;*.png`, then `a.jpg`, `B.PNG` and `.jpg` are included and `a.jpg.bak`, `jpg` and `a.gif` are not.
   Parts are separated by `;`, white space around a part is ignored (`*.jpg ; *.png` is the same), empty parts are ignored
   (`;*.jpg;;`), and the case is ignored (ordinal comparison, no culture rules).
2. Given `?`, then it stands for exactly one character: `IMG_???.jpg` includes `IMG_001.jpg` and not `IMG_01.jpg` or
   `IMG_0001.jpg`. `*` stands for any text, empty included: `a*` includes `a` and `a.txt`; `*a*b*` includes `xaxbx` and not
   `xbxax`. Every other character is literal, `[`, `(`, `+`, `.` and `$` included (`a(1).txt` includes only `a(1).txt` and
   `A(1).TXT`).
3. Given `*.*`, then every name is included, the ones with no dot too (as Windows reads it); given `*`, the same. Only the whole part `*.*` is special: `a*.*` is read as `a`, any text, a literal dot, any text (as
   criterion 2), so it does not include `a` or `abc`, and includes `a.` and `abc.txt`. A mask of
   only spaces or only `;` has no condition. Given a part with no wildcard (`readme.txt`), then only that name is included.
4. Given a mask and a folder item `Trip 2024` with mask `Trip*`, then it is included; with mask `*.jpg` it is not.

Regex (matched against the base name: no extension, the whole name for a folder or a dotfile, as in NS-1):
5. Given pattern `^IMG_\d+` and `IMG_042.jpg`, `img_1.jpg` and `a_IMG_1.jpg`, then only the first is included; with
   `ignoreCase` true, the first two. The match is a search (`IsMatch`), so anchors are the user's job: `42` includes
   `IMG_042.jpg`. The pattern is built once, with `CultureInvariant` and a 100 ms match timeout, as in NS-24.
6. Given pattern `jpg` and `a.jpg`, then it is not included: the extension is not searched.
7. Given an invalid pattern (`(`, `[a-`, `*a`), then `Error` is a non-empty message (the parser's own, from
   `RegexRule.Validate`; tests check only that it is not empty) and the pattern condition is ignored: the other conditions
   still apply, so a hidden item stays out with the default `includeHidden`. A valid or empty pattern gives `Error` null.
8. Given a pattern that takes too long on a name (`(a+)+$` on `aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaab.txt`), then that item
   is not included, and `Includes` does not throw and `Error` stays null; other items are judged on their own.

Kinds and hidden:
9. Given `includeFolders` false, then folder items are out and files in; given `includeFiles` false, then files are out and
   folders in; given both false, then nothing is included; given both true (default), both kinds are in.
10. Given an item with the `Hidden` attribute or the `System` attribute (or both), then it is out with `includeHidden` false
    (default) and in with it true. `ReadOnly` has no effect. The check is on `ItemAttributes`, not on the name: `.gitignore`
    with no attribute is not hidden.

Length (of the base name, in UTF-16 characters: an emoji counts 2, the same as `string.Length`):
11. Given `minLength` 5 and `maxLength` 8, then `abcd.txt` (4), is out, `abcde.txt` (5) and `abcdefgh.txt` (8) are in and
    `abcdefghi.txt` (9) is out. With only a minimum or only a maximum the other side is open. For a folder or a dotfile the
    whole name is counted (`.gitignore` is 10).
12. Given `minLength` 0, then every name is in. Given `minLength` greater than `maxLength`, then `Error` is the text `The
    minimum length is larger than the maximum length.` and both length limits are ignored (the other conditions apply).
    Given `minLength` equal to `maxLength`, then only names of exactly that length are in. Given an invalid pattern and
    `minLength` above `maxLength` together, then `Error` holds both texts, the pattern's message first, joined by one
    space (`<pattern message> The minimum length is larger than the maximum length.`); both bad parts are ignored. A test
    checks that `Error` ends with the length text and is longer than it.

Together:
13. Given mask `*.jpg`, pattern `^IMG`, `minLength` 6, `includeFolders` false, then an item must pass all of them: a folder
    `IMG_001.jpg` is out, `IMG_01.jpg` (base 6) is in, `IMG_1.jpg` (base 5) is out, `DSC_0001.jpg` is out.
14. Given the properties, then each returns the value given (`Mask` and `Pattern` as typed, never trimmed).

Excluded items still count:
15. Given `a.png` and `a.jpg` in `C:\x` with mask `*.png`, `Apply` returns only `a.png`; given the pipeline with
    `ExtensionRule(ExtensionMode.Fixed, "jpg")` on that list and `DuplicateTargetCheck` with the existing names `a.png` and
    `a.jpg` of `C:\x` (the whole folder, as the app gives them), then `a.png` gets `TargetExists`. The filter has no
    knowledge of existing names: the caller keeps passing every name of the folder to the check (NS-14, NS-6).
16. Review check, not a test: the mask and the pattern are built once in the constructor, not per item (no time limit is
    tested, as in NS-58).

## Out of scope
- The panel, the sub-folders option and listing hidden files (NS-33).
- Command-line options (NS-41) and saving in presets (NS-36).
- Excluding by pattern (a "not" mask), filters on size or date, matching the full path.
- Counting characters by text element or code point.

## Decisions
- Match against name or full name? **Decision:** the original name with extension for masks, and the base name for regex
  and length, since masks are usually about extensions (po, 2026-10-18).
- Wildcards? **Decision:** only `*` and `?`, with `*.*` meaning every name: what users of Explorer and older renamers
  expect; no character classes (po, 2026-10-24).
- Invalid regex? **Decision:** the bad part alone is ignored and reported; the other conditions keep applying, so a bad
  pattern never adds hidden files or folders to the batch. (The first version said "excludes nothing"; this is safer and
  the story's other criteria are unchanged.) Same for a minimum above the maximum (po, 2026-10-24).
- Regex timeout? **Decision:** an item whose match times out is left out, not an error: not renaming is the safe side
  (po, 2026-10-24).
- Regex case? **Decision:** case-sensitive by default, with `ignoreCase`, the same as the RegEx rule (po, 2026-10-24).
- Kinds? **Decision:** two switches (files, folders), both on by default; both off includes nothing, as asked (po,
  2026-10-24).
- Hidden? **Decision:** off by default, and it covers the `System` attribute too, as the file list does today (po,
  2026-10-24).
- Where does the filter run? **Decision:** before the pipeline, on the list. The pipeline is unchanged: numbering then
  follows the included items in list order, as with a selection (NS-14) (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: criteria made testable against `RenameItem`, `NameParts`, `RegexRule.Validate` and `DuplicateTargetCheck`; invalid-regex rule refined; set Ready (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #54.
