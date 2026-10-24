# CLAUDE.md: NameShift

Free, open-source, lightweight bulk file renamer for Windows (MIT). Owner: **David**. Repo: `Squad-G/NameShift`
(public). Local: `D:\Projects\Softwares\NameShift`.

## Stack
- C# (latest language version), **WinForms on .NET Framework 4.8** (ships with Windows 10/11: no runtime to bundle).
- `src/NameShift.Core` (netstandard2.0): the rename engine, no UI dependency. `src/NameShift.App` (net48): the window.
  `tests/NameShift.Core.Tests` (net10.0 and net48, xUnit): engine tests.
- `src/NameShift.Disk` (netstandard2.0): the rename executor, the app's only code that renames files.
  `tests/NameShift.Disk.Tests` (net10.0 and net48, xUnit): executor tests against real temp folders.
- `src/NameShift.Ui` (netstandard2.0): window logic (navigation, path parsing, tree model, disk folder source) and
  every visible string (`Strings.resx`), no WinForms reference. `tests/NameShift.Ui.Tests` (net10.0 and net48, xUnit).
- Installer: Inno Setup 6 (`installer/`). Target: a download of a few MB at most.
- No third-party UI framework. A new NuGet dependency needs a reason in the PR (size, and an MIT-compatible licence).

### Window code
- Each window area is a `UserControl` in `src/NameShift.App/Controls/`, docked into a named host panel of `MainForm`;
  `MainForm` only composes and wires.
- Logic goes in a `NameShift.Ui` class with a test; the App keeps thin controls that render and forward input.
- Routine feedback goes to the status bar (`MainForm.ShowStatus`), never a `MessageBox`.
- Disk access the UI triggers runs off the UI thread (`Task.Run`); apply the result on the UI thread after `await`.
- Every visible string comes from `NameShift.Ui.Strings`. Controls are built in code (no designer files), sizes in
  96-DPI logical pixels.

## Rules
- **Clean-room:** NameShift reproduces the *behaviour* of classic bulk renamers. Never copy another product's code,
  text, icons or name.
- **Never rename a real file without a preview and a way to undo.** The engine computes names; only the app touches the
  disk, and it logs every batch for Revert.
- **Never push to `dev` or `prod` directly; never touch `releases/**`.** Branch from `dev` (`feature/<ID>-<slug>`,
  `fix/…`, `chore/…`) and open a PR to `dev`. Releases are David's only (`docs/RELEASING.md`).
- **Commits and PR titles:** `[<ID>] summary` for a story (`[NS-12] Numbering rule`), `[chore]` or `[docs]` otherwise.
- **No AI attribution** in commits, PR titles, descriptions or reviews.
- One concern per PR. Measure before optimising.

## Checks (all must pass; warnings are errors)
```
dotnet build -c Release
dotnet test -c Release --no-build
```
At least one test per acceptance criterion the engine can check. UI-only criteria are checked by hand and listed in
the PR.

## Roles
| Role | Who | Decides / does |
|---|---|---|
| Owner | David | Releases; can override any decision. |
| Lead | the main Claude session | Launches the agents, relays questions to David, commits the `po`'s story changes (branch `chore/stories-<topic>`, PR to `dev`, reviewed and merged like any PR). |
| Product | `po` agent | Product questions and backlog order, from `stories/` and `docs/PLAN.md`; makes stories `Ready`. |
| Tech | `tech-lead` agent | Technical approach, builder briefs, and **merging** approved PRs into `dev`. |
| Review | `reviewer` agent | Independent review of every PR; posts `VERDICT: APPROVE` or `VERDICT: CHANGES REQUESTED`. |
| Build | general-purpose Sonnet agents | Implement one story from its brief, open the PR. |

## Flow
1. `po` writes or refines stories, answers open questions, orders the backlog, and sets the next stories to `Ready`.
2. `tech-lead` writes one brief per Ready story.
3. A builder sets the story to `In Progress` in its branch's first commit, implements it, runs the checks, opens the
   PR and sets the story to `In Review`.
4. `reviewer` reviews (a fresh context, never the builder's).
5. On `VERDICT: APPROVE` with green checks, `tech-lead` marks the story `Done` in a last commit on the branch and
   squash-merges into `dev`. On `CHANGES REQUESTED`, the builder fixes and the reviewer re-reviews.

Stories: `stories/README.md`.
