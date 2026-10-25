# Backlog

Ordered, highest priority first. The first rows (Ready) are the minimal usable slice: preview, safe rename and undo
with Replace, Case, Add and Numbering. Everything after it is the long tail. The `#` column is the story number (stable
IDs), not the rank: the rank is the row order.

Order after NS-37 (po, 2026-10-25): NS-81, NS-84 and NS-80 are Done; then safety and trust (NS-91
hardening, NS-90 and NS-45 safer Revert), then the Explorer launch path (NS-88, NS-89), then the photo date (NS-62, then
NS-85 and NS-86) and the command line (NS-93 cleanup first, then NS-41 filters, then NS-87), then the small chores (NS-92, NS-94). NS-49 and
NS-44 (the registry entry and the installer) are built together at the very end, just before the v1 release (David,
2026-10-25); NS-44 is the last story of v1. NS-50 (Windows 11 compact menu) was cancelled by David on 2026-10-25. NS-85 is merged and NS-86 is Ready, but
NS-86 and NS-87 both edit the command line's apply path: NS-87 is being built now, and NS-86 starts after NS-87 merges.

| # | Story | Status | Depends on |
|---|---|---|---|
| 1 | [NS-1 Rename pipeline and rule contract](NS-1-pipeline.md) | Done | |
| 2 | [NS-2 Replace rule](NS-2-replace.md) | Done | NS-1 |
| 3 | [NS-3 Case rule](NS-3-case.md) | Done | NS-1 |
| 4 | [NS-4 Add rule](NS-4-add.md) | Done | NS-1 |
| 17 | [NS-17 Numbering rule (basic)](NS-17-numbering.md) | Done | NS-1 |
| 5 | [NS-5 Invalid Windows names check](NS-5-invalid.md) | Done | NS-1 |
| 6 | [NS-6 Duplicate target detection](NS-6-duplicates.md) | Done | NS-1 |
| 7 | [NS-7 Path length check](NS-7-pathlength.md) | Done | NS-1 |
| 8 | [NS-8 Safe rename executor](NS-8-executor.md) | Done | NS-1, NS-5, NS-6, NS-7 |
| 9 | [NS-9 Undo journal and Revert](NS-9-journal.md) | Done | NS-8 |
| 10 | [NS-10 Window shell: path bar and folder tree](NS-10-windowshell.md) | Done | |
| 51 | [NS-51 File list with folders and natural sort](NS-51-filelist.md) | Done | NS-10 |
| 11 | [NS-11 Rule panel host: enable and reset per rule](NS-11-panelhost.md) | Done | NS-10, NS-1 |
| 12 | [NS-12 Panels for Replace, Case and Add](NS-12-firstpanels.md) | Done | NS-11, NS-2, NS-3, NS-4 |
| 53 | [NS-53 Numbering panel](NS-53-numberingpanel.md) | Done | NS-11, NS-17 |
| 13 | [NS-13 Live preview](NS-13-preview.md) | Done | NS-51, NS-11, NS-1 |
| 52 | [NS-52 Conflict display](NS-52-conflictdisplay.md) | Done | NS-13, NS-5, NS-6, NS-7 |
| 14 | [NS-14 Choose which files to rename](NS-14-selection.md) | Done | NS-51, NS-13 |
| 15 | [NS-15 Rename, Revert and Reset buttons](NS-15-buttons.md) | Done | NS-13, NS-52, NS-14, NS-9 |
| 56 | [NS-56 Folder tree follows Rename and Revert](NS-56-treerefresh.md) | Done | NS-10, NS-15 |
| 57 | [NS-57 Rename stays off until something changes](NS-57-renameonce.md) | Done | NS-15 |
| 18 | [NS-18 Extension rule](NS-18-extension.md) | Done | NS-1 |
| 19 | [NS-19 Name rule: keep, remove, fixed, reverse](NS-19-namerule.md) | Done | NS-1 |
| 20 | [NS-20 Remove: first n, last n, from-to](NS-20-removepos.md) | Done | NS-1 |
| 58 | [NS-58 Steady 10,000-file listing test](NS-58-flakyperf.md) | Done | NS-51 |
| 16 | [NS-16 Replace rule: keep the case pattern of the found text](NS-16-replacecase.md) | Done | NS-2, NS-12 |
| 55 | [NS-55 Panels for Name and Extension](NS-55-namepanels.md) | Done | NS-11, NS-18, NS-19, NS-16 |
| 21 | [NS-21 Remove: characters, text, crop before/after](NS-21-removetext.md) | Done | NS-1 |
| 22 | [NS-22 Remove: digits, symbols, accents, high characters](NS-22-removeclasses.md) | Done | NS-1 |
| 23 | [NS-23 Remove: trim, leading dots, double spaces](NS-23-removetidy.md) | Done | NS-1 |
| 30 | [NS-30 Panel for Remove](NS-30-morepanels.md) | Done | NS-11, NS-20, NS-21, NS-22, NS-23 |
| 24 | [NS-24 RegEx rule](NS-24-regex.md) | Done | NS-1 |
| 28 | [NS-28 Append folder name rule](NS-28-foldername.md) | Done | NS-1 |
| 64 | [NS-64 Tests with a lone surrogate must really use one](NS-64-surrogatetests.md) | Done | NS-21 |
| 63 | [NS-63 Run the engine tests on .NET Framework 4.8 too](NS-63-coretestsnet48.md) | Done | NS-1, NS-28 |
| 25 | [NS-25 Move or copy parts of the name](NS-25-moveparts.md) | Done | NS-1 |
| 26 | [NS-26 Auto date rule (file dates)](NS-26-autodate.md) | Done | NS-1 |
| 31 | [NS-31 Panels for RegEx and Move/Copy](NS-31-lastpanels.md) | Done | NS-11, NS-24, NS-25 |
| 59 | [NS-59 Panels for Auto date and Append folder name](NS-59-datefolderpanels.md) | Done | NS-11, NS-26, NS-28, NS-31 |
| 29 | [NS-29 Numbering rule: hex, letters and roman numerals](NS-29-numberingadv.md) | Done | NS-17 |
| 27 | [NS-27 Read the photo (EXIF) date from a JPEG](NS-27-exifdate.md) | Done | NS-1 |
| 61 | [NS-61 Numbering panel: number system](NS-61-numberingpanel2.md) | Done | NS-53, NS-29 |
| 47 | [NS-47 Open a folder from the command line or by drag and drop](NS-47-openpath.md) | Done | NS-10, NS-51 |
| 46 | [NS-46 Clear row messages](NS-46-rowhelp.md) | Done | NS-52 |
| 32 | [NS-32 Filters: mask, regex, kinds and length](NS-32-filterengine.md) | Done | NS-1 |
| 65 | [NS-65 README layout matches the repository](NS-65-readmelayout.md) | Done | |
| 66 | [NS-66 Column sorting](NS-66-columnsort.md) | Done | NS-51, NS-14, NS-13 |
| 48 | [NS-48 Hard cases: names that look the same](NS-48-hardcases.md) | Done | NS-6 |
| 67 | [NS-67 Hard cases on disk: read-only, in use, vanishing drive](NS-67-diskhardcases.md) | Done | NS-9, NS-15 |
| 73 | [NS-73 One planner for the window and the command line](NS-73-renameplanner.md) | Done | NS-13, NS-52, NS-15 |
| 38 | [NS-38 Command line: apply rules to a folder](NS-38-clicore.md) | Done | NS-8, NS-9, NS-12, NS-13, NS-73 |
| 54 | [NS-54 Visual polish](NS-54-visual-polish.md) | Done | NS-15, NS-55, NS-30 |
| 60 | [NS-60 Numbering rule: restart in each folder](NS-60-numberingfolders.md) | Done | NS-17, NS-29 |
| 33 | [NS-33 Filters: settings model and filtered file list](NS-33-filterui.md) | Done | NS-32, NS-51, NS-66, NS-73 |
| 36 | [NS-36 Presets: format and store](NS-36-presets.md) | Done | NS-12, NS-55, NS-30, NS-31, NS-59 |
| 43 | [NS-43 Command line: help, version, console text and the exit code of a crash](NS-43-clihelp.md) | Done | NS-38 |
| 71 | [NS-71 Numbering panel: restart in each folder](NS-71-numberingrestart.md) | Done | NS-60, NS-61 |
| 72 | [NS-72 Presets menu: save, load and delete](NS-72-presetmenu.md) | Done | NS-36, NS-15 |
| 74 | [NS-74 Sub-folders: recursive, cancellable listing](NS-74-subfolderlisting.md) | Done | NS-33, NS-66, NS-73 |
| 70 | [NS-70 Filter row above the file list](NS-70-filterrow.md) | Done | NS-33 |
| 42 | [NS-42 Command line: undo the last batch](NS-42-cliundo.md) | Done | NS-38, NS-9, NS-43 |
| 39 | [NS-39 Command line: quiet and verbose output](NS-39-clidry.md) | Done | NS-38, NS-43, NS-42 |
| 40 | [NS-40 Command line: Add and Numbering options](NS-40-clirules.md) | Done | NS-38, NS-12, NS-53, NS-61, NS-43, NS-42, NS-39 |
| 69 | [NS-69 Command line: use a saved preset](NS-69-clipreset.md) | Done | NS-38, NS-36, NS-43, NS-72, NS-42, NS-39, NS-40 |
| 79 | [NS-79 Command line: split the apply parser by panel](NS-79-parseapplysplit.md) | Done | NS-40, NS-69 |
| 75 | [NS-75 Command line: Name, Extension and Remove options](NS-75-clirules2.md) | Done | NS-40, NS-69, NS-79, NS-55, NS-30 |
| 76 | [NS-76 Command line: RegEx, Move/Copy, Auto date and Folder name options](NS-76-clirules3.md) | Done | NS-75, NS-31, NS-59 |
| 78 | [NS-78 Sub-folders box and Folder column](NS-78-subfolderbox.md) | Done | NS-70, NS-74 |
| 82 | [NS-82 Open a folder from the file list](NS-82-openfolderrow.md) | Done | NS-10, NS-51, NS-78 |
| 77 | [NS-77 Find and fix the occasional net48 failure in the Ui tests](NS-77-uitestsflake.md) | Done | |
| 83 | [NS-83 Collapsible rule panels, and a bottom row that fits](NS-83-collapsepanels.md) | Done | NS-11, NS-15 |
| 37 | [NS-37 Remember last settings](NS-37-settings.md) | Done | NS-36, NS-72, NS-83 |
| 84 | [NS-84 Remember which panels are collapsed](NS-84-collapsesaved.md) | Done | NS-83, NS-37 |
| 80 | [NS-80 Fix raw control characters in a test and guard against them](NS-80-controlchars.md) | Done | |
| 81 | [NS-81 Pin the exit code of a refused undo that leaves the history unusable](NS-81-undoexit.md) | Done | NS-42 |
| 34 | [NS-34 Copy or move to another folder (engine and executor)](NS-34-copymove.md) | Cancelled | NS-8, NS-9 |
| 35 | [NS-35 Copy/move to location panel](NS-35-copymoveui.md) | Cancelled | NS-34, NS-15 |
| 50 | [NS-50 "Bulk Rename Here" in the Windows 11 top-level menu](NS-50-win11-menu.md) | Cancelled | NS-49 |
| 91 | [NS-91 Settings, presets and history: deep files, leftover temp files, no profile folder](NS-91-filehardening.md) | Done | NS-37, NS-36, NS-9 |
| 90 | [NS-90 Undo journal: remember each file's size and date, and check before Revert](NS-90-journalfingerprint.md) | Done | NS-9 |
| 45 | [NS-45 Safer Revert: detect changed files](NS-45-revertsafe.md) | Done | NS-90, NS-15, NS-42 |
| 88 | [NS-88 Start on several files: one folder, those items selected](NS-88-multipath.md) | Done | NS-47, NS-14 |
| 89 | [NS-89 One window at all times](NS-89-singleinstance.md) | Done | NS-88 |
| 62 | [NS-62 Auto date: the photo date in the engine](NS-62-autodatephoto.md) | Done | NS-26, NS-27 |
| 93 | [NS-93 Command line readers: remove the copies](NS-93-clireadercleanup.md) | Done | NS-40, NS-69, NS-75, NS-76 |
| 41 | [NS-41 Command line: filters](NS-41-clifilters.md) | Done | NS-38, NS-32, NS-33, NS-70, NS-79, NS-76, NS-93 |
| 85 | [NS-85 Read the photo dates of the listed files](NS-85-photodates.md) | Done | NS-62, NS-27 |
| 86 | [NS-86 Auto date panel: the Photo date choice](NS-86-autodatephotopanel.md) | Ready | NS-62, NS-85, NS-87, NS-59, NS-76 |
| 87 | [NS-87 Command line: --recurse](NS-87-clirecurse.md) | Ready | NS-41, NS-74, NS-78 |
| 92 | [NS-92 Move the settings-saving logic out of MainForm into NameShift.Ui](NS-92-savesettingslogic.md) | Ready | NS-37, NS-84 |
| 94 | [NS-94 Close gaps in the tests: PathInput prefixes, both orders of the list model, broken path literals](NS-94-testgaps.md) | Done | NS-10, NS-70, NS-78, NS-80 |
| 49 | [NS-49 Explorer right-click entry "Bulk Rename Here": the registry keys](NS-49-explorer-entry.md) | Draft | NS-88, NS-89 |
| 44 | [NS-44 Installer](NS-44-installer.md) | Draft | NS-15, NS-38, NS-49 |