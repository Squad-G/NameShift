# NS-24: RegEx rule

Status: Done
Depends on: NS-1
Area: engine

## Why
Powerful match and replace for advanced users.

## Acceptance criteria
`RegexRule(string pattern, string replacement, bool ignoreCase = false, bool includeExtension = false)`, stage RegEx
(1: it runs before every other rule). The pattern is a .NET regular expression, built once in the constructor with
`RegexOptions.CultureInvariant` (plus `IgnoreCase` when asked), no `Multiline`, no `Compiled`, and a match timeout of
100 ms. The replacement uses the .NET substitution syntax (`$1`, `${name}`, `$&`, `$$`). Every match is replaced,
left to right. Only the base name is searched unless `includeExtension` (then the whole name, re-split into base and
extension afterwards, as `ReplaceRule` does).
1. Given pattern `IMG_(\d+)` and replacement `Photo $1` on `IMG_042.jpg`, then `Photo 042.jpg`. Given a named group,
   `(?<n>\d+)` with `x${n}`, on `a12.txt`, then `ax12.txt`.
2. Given an invalid pattern (`(`, `[a-`, `*a`), then `RegexRule.Validate(pattern)` returns a non-empty message (the
   parser's own message; its wording differs between runtimes and language packs) and the constructor throws
   `ArgumentException`; a valid pattern gives `Validate` null.
   The check belongs to the panel (NS-31), which marks the pattern field once and builds no rule: a bad pattern never
   produces a per-file error. Given a null pattern or replacement, the constructor throws `ArgumentNullException`.
3. Given `includeExtension` off (default), then the extension is never searched or changed: pattern `txt` on
   `txt.txt` gives `.txt` after replacing with an empty string (the base `txt` goes, the extension stays). Given it on,
   the pattern runs on the full name: pattern `\.jpe?g$` with `.jpg` on `a.jpeg` gives `a.jpg`; pattern `^(.*)\.(.*)$`
   with `$2.$1` on `a.txt` gives `txt.a` (split again: base `txt`, extension `a`).
4. Given `ignoreCase` on, then `img` matches `IMG_1.jpg` (pattern `img`, replacement `x`, result `x_1.jpg`); given it
   off (default), then nothing matches and the name is unchanged. Casing is invariant-culture (no Turkish `i`).
5. Given a pattern that takes too long (`(a+)+$` on `aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaab.txt`), then it stops after
   100 ms and the item gets a rename `Error` whose message says the pattern took too long on this name (the rule
   throws `InvalidOperationException` with that English text, wrapping `RegexMatchTimeoutException`; the pipeline turns
   it into the item's error, as for any rule). The other items of the same run are not affected. The test finishes
   within a few seconds.
6. Given an empty pattern, then the rule does nothing and `Enabled` is false; an empty replacement is allowed and
   removes each match (`\d+` with `` on `a12b.txt` gives `ab.txt`).
7. Given a replacement that references a group that does not exist (`$9`), then the text is kept as typed, as .NET
   does; given a pattern that matches the empty string (`x*` with `-` on `ab.txt`), then .NET's behaviour applies
   (`-a-b-.txt`), no error and no endless loop.
8. Given a result that is empty, then a file keeps its extension (`.txt`, valid per NS-5, not flagged) and a folder
   gives an empty name flagged `EmptyName` by the existing invalid-name check (pipeline test with `InvalidNameCheck`).
   A result with a Windows-invalid character (`a:b`) is applied as typed and flagged by the check.
9. Given a folder `Folder 1.2` with pattern `\d` and `#`, then `Folder #.#` (a folder's whole name is the base, NS-1);
   given a hidden dotfile `.gitignore` with `^\.` and an empty replacement, then `gitignore`.
10. Given the pipeline with this rule and a Replace rule (stage 3), then this rule runs first (stage 1): pattern
    `a` with `b` then Replace `b` with `c` on `a.txt` gives `c.txt`.
11. Given a name with an emoji (a surrogate pair) or a lone surrogate, and the pattern `a`, then the rule runs without
    throwing and returns the name with only the matches replaced. The regex works on UTF-16 units, so a pattern such
    as `.` can match half a pair; the rule does not guard against that (no test asserts it).
12. Given the properties, then `Pattern`, `Replacement`, `IgnoreCase` and `IncludeExtension` return the values given.

## Out of scope
- The panel (NS-31) and its error display.
- A regex tester, saved patterns, a cheat sheet.
- Other flavours (PCRE, JavaScript).
- Lookup against the file's content or dates.

## Decisions
- Which regex flavour? **Decision:** the .NET one, because it is built in and the panel says so (po, 2026-10-18).
- Ignore case by default? **Decision:** off: regular expression users expect an exact match, and the panel has a
  checkbox. Replace is the opposite on purpose (po, 2026-10-23).
- Where is a bad pattern reported? **Decision:** once, in the panel, through `Validate`; the rule cannot be built with
  one, so no item is ever marked for it (po, 2026-10-23).
- Timeout? **Decision:** 100 ms per name, so a pathological pattern cannot freeze the preview; the item shows an error
  and the rest still works (po, 2026-10-18).
- No `Compiled`, no `Multiline`: compiling costs startup time for a few names; `^` and `$` mean the start and end of
  the searched text (po, 2026-10-23).
- Empty result on a file? **Decision:** `.txt` is valid (NS-5); only a folder is flagged (po, 2026-10-23).

## Log
- 2026-10-18: created (po).
- 2026-10-23: criteria made testable (options, validation, timeout, group syntax, edge cases), set Ready (po).
- 2026-10-24: In Progress (builder).
- 2026-10-24: In Review (builder), PR #38.
- 2026-10-24: Done (tech-lead), PR #38 merged into dev; review nits folded (no-Multiline test, 100 ms timeout pinned).
