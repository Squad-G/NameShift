# Backlog

Ordered, highest priority first. The first rows (Ready) are the minimal usable slice: preview, safe rename and undo
with Replace, Case, Add and Numbering. Everything after it is the long tail. The `#` column is the story number (stable
IDs), not the rank: the rank is the row order.

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
| 66 | [NS-66 Column sorting](NS-66-columnsort.md) | Ready | NS-51, NS-14, NS-13 |
| 48 | [NS-48 Hard cases: names that look the same](NS-48-hardcases.md) | Ready | NS-6 |
| 67 | [NS-67 Hard cases on disk: read-only, in use, vanishing drive](NS-67-diskhardcases.md) | Ready | NS-9, NS-15 |
| 73 | [NS-73 One planner for the window and the command line](NS-73-renameplanner.md) | Ready | NS-13, NS-52, NS-15 |
| 38 | [NS-38 Command line: apply rules to a folder](NS-38-clicore.md) | Ready | NS-8, NS-9, NS-12, NS-13, NS-73 |
| 54 | [NS-54 Visual polish](NS-54-visual-polish.md) | Ready | NS-15, NS-55, NS-30 |
| 60 | [NS-60 Numbering rule: restart in each folder](NS-60-numberingfolders.md) | Ready | NS-17, NS-29 |
| 33 | [NS-33 Filters: settings model and filtered file list](NS-33-filterui.md) | Ready | NS-32, NS-51, NS-66, NS-73 |
| 36 | [NS-36 Presets: format and store](NS-36-presets.md) | Ready | NS-12, NS-55, NS-30, NS-31, NS-59 |
| 74 | [NS-74 Sub-folders: recursive, cancellable listing](NS-74-subfolderlisting.md) | Draft | NS-33, NS-66, NS-73 |
| 70 | [NS-70 Filter row above the file list](NS-70-filterrow.md) | Draft | NS-33, NS-74 |
| 71 | [NS-71 Numbering panel: restart in each folder](NS-71-numberingrestart.md) | Draft | NS-60, NS-61 |
| 72 | [NS-72 Presets menu: save, load and delete](NS-72-presetmenu.md) | Draft | NS-36, NS-15 |
| 37 | [NS-37 Remember last settings](NS-37-settings.md) | Draft | NS-36, NS-72 |
| 34 | [NS-34 Copy or move to another folder (engine and executor)](NS-34-copymove.md) | Cancelled | NS-8, NS-9 |
| 35 | [NS-35 Copy/move to location panel](NS-35-copymoveui.md) | Cancelled | NS-34, NS-15 |
| 39 | [NS-39 Command line: quiet and verbose output](NS-39-clidry.md) | Draft | NS-38 |
| 40 | [NS-40 Command line: rules given as options](NS-40-clirules.md) | Draft | NS-38, NS-12 |
| 69 | [NS-69 Command line: use a saved preset](NS-69-clipreset.md) | Draft | NS-38, NS-36 |
| 41 | [NS-41 Command line: recursion and filters](NS-41-clifilters.md) | Draft | NS-38, NS-32, NS-33, NS-74 |
| 45 | [NS-45 Safer Revert: detect changed files](NS-45-revertsafe.md) | Draft | NS-9 |
| 42 | [NS-42 Command line: undo the last batch](NS-42-cliundo.md) | Draft | NS-38, NS-45 |
| 43 | [NS-43 Command line: help and version](NS-43-clihelp.md) | Draft | NS-38 |
| 49 | [NS-49 Explorer right-click entry "Bulk Rename Here"](NS-49-explorer-entry.md) | Draft | NS-47, NS-14 |
| 44 | [NS-44 Installer](NS-44-installer.md) | Draft | NS-15, NS-38, NS-49 |
| 62 | [NS-62 Auto date: use the photo date](NS-62-autodatephoto.md) | Draft | NS-26, NS-27, NS-59 |
| 50 | [NS-50 "Bulk Rename Here" in the Windows 11 top-level menu](NS-50-win11-menu.md) | Draft | NS-49 |
