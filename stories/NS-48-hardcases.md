# NS-48: Hard cases: names that look the same

Status: In Progress
Depends on: NS-6
Area: engine

## Why
Two names can look identical on screen and still be different to NTFS (`é` as one character, or `e` plus a combining
accent). The user cannot see the difference, so the preview must treat them as the same name. (The disk-side hard cases are
NS-67.)

## Acceptance criteria
Change: `DuplicateTargetCheck` (`NameShift.Core`). Names are compared by their canonical (NFC) form, then ordinal ignoring
case, for the new names, the existing names of the folder and the batch's original names. Nothing else in the checks changes.
1. Given two changed items whose new names are `café.txt` and `café.txt`, then both are flagged
   `DuplicateTarget` (and nothing else).
2. Given one changed item with new name `café.txt` and an existing entry `café.txt` that is not in the batch, then
   the item is flagged `TargetExists`; the same with the forms swapped.
3. Given the forms differ in case too (`CAFÉ.txt` and `café.txt`), then they are flagged as duplicates.
4. Given one item `café.txt` whose only change is to the other form, `café.txt`, and no other item or entry has the
   name, then it is a normal change: no conflict (the item's own original name is released, compared by the same key).
5. Given an item whose name is renamed to the NFC-equal name of another batch item that is itself renamed away in the same
   batch (a chain), then it is not flagged, as for plain names today.
6. Given a new name with a lone surrogate (`"a\uD800"`), then the check does not throw; that name is compared as it is
   (ordinal ignoring case), so two equal such names are still flagged `DuplicateTarget`.
7. Given names with no accent or special form (the existing NS-6 tests), then the results are identical to before.
8. Given a 10,000-item batch, then the check still runs in linear time (a loose test under 1 second).
9. Given an unchanged item (status Unchanged), even with a name over 200 characters that Windows allows, then none of the
   three checks flags it, and it is not touched. (Already true: this criterion pins it with a test for the three checks
   on a 250-character name.)

## Notes
- Two real files named `café.txt` in NFC and NFD form in one folder would hide each other once one of them is renamed
  away (the check keys them as one name). This is safe: the executor's real disk check (NS-8) still refuses a rename onto an
  existing entry. Accepted.

## Out of scope
- Disk behaviour (read-only, in use, drive vanishing): NS-67.
- Changing the file's name to its NFC form on purpose (a rule for it, if ever wanted, is a new story).
- Comparing compatibility forms (`ﬁ` and `fi`): they are different names on screen.

## Decisions
- Treat canonically equal names as equal? **Decision:** yes, flag them, because the user sees one name and a second file
  that looks the same is a trap; it is the safe side (po, 2026-10-24).
- Must the executor also refuse them? **Decision:** no: the preview already blocks the batch (the executor refuses any plan
  with a conflict, NS-8), and NTFS can hold both names, so the executor keeps checking the real disk only (po, 2026-10-24).
- Lone surrogates: `string.Normalize` throws on them, so the key falls back to the raw name instead of failing the preview
  (po, 2026-10-24).
- Split? **Decision:** the original criteria 2 to 5 are disk behaviour and moved to NS-67 (po, 2026-10-24).

## Log
- 2026-10-18: created (po).
- 2026-10-24: criteria made testable, disk cases split to NS-67; set Ready (po).
- 2026-10-24: after review: note on NFC/NFD twins added (po).
- 2026-10-24: In Progress (builder).
