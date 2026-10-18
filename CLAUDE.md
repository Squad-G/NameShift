# CLAUDE.md: NameShift

Free, open-source, lightweight bulk file renamer for Windows (MIT). Owner: **David**. Repo: `Squad-G/NameShift`
(public). Local: `D:\Projects\Softwares\NameShift`.

## Stack
- C# (latest language version), **WinForms on .NET Framework 4.8** (ships with Windows 10/11: no runtime to bundle).
- `src/NameShift.Core` (netstandard2.0): the rename engine, no UI dependency. `src/NameShift.App` (net48): the window.
  `tests/NameShift.Core.Tests` (net10.0, xUnit): engine tests.
- Installer: Inno Setup 6 (`installer/`). Target: a download of a few MB at most.
- No third-party UI framework. A new NuGet dependency needs a reason in the PR (size, and an MIT-compatible licence).

## Rules
- **Clean-room:** NameShift reproduces the *behaviour* of classic bulk renamers. Never copy another product's code,
  text, icons or name.
- **Never rename a real file without a preview and a way to undo.** The engine computes names; only the app touches the
  disk, and it logs every batch for Revert.
- **Never push to `dev` or `prod` directly; never touch `releases/**`.** Branch from `dev` (`feature/<ID>-<slug>`,
  `fix/…`, `chore/…`) and open a PR to `dev`. Releases are David's only (`docs/RELEASING.md`).
- **No AI attribution** in commits, PR titles or descriptions.
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
| Product | `po` agent | Product questions and backlog order, from `stories/` and `docs/PLAN.md`; makes stories `Ready`. |
| Tech | `tech-lead` agent | Technical approach, builder briefs, and **merging** approved PRs into `dev`. |
| Review | `reviewer` agent | Independent review of every PR; posts `VERDICT: APPROVE` or `VERDICT: CHANGES REQUESTED`. |
| Build | general-purpose Sonnet agents | Implement one story from its brief, open the PR. |

## Flow
1. `po` writes or refines stories, answers open questions, orders the backlog, and sets the next stories to `Ready`.
2. `tech-lead` writes one brief per Ready story.
3. A builder implements it on its branch, runs the checks, opens the PR, sets the story to `In Review`.
4. `reviewer` reviews (a fresh context, never the builder's).
5. On `VERDICT: APPROVE` with green checks, `tech-lead` marks the story `Done` in a last commit on the branch and
   squash-merges into `dev`. On `CHANGES REQUESTED`, the builder fixes and the reviewer re-reviews.

Stories: `stories/README.md`.
