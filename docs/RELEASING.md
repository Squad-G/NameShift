# Publishing a release

Releases are made by hand, by the repo owner only. GitHub refuses anyone else's push to `prod` or `releases/**`.

## How the branches work

| Branch | What it holds | Who can change it |
|---|---|---|
| `dev` | Work in progress; pull requests land here | Anyone, through a pull request (no deletion, no force push) |
| `prod` | The latest released version | Only the owner |
| `releases/vX.Y.Z` | One branch per release, frozen | Only the owner can create one; nobody can change or delete it afterwards |

A release moves `prod` up to `dev`, then creates `releases/vX.Y.Z` at that same commit, so every old version stays
available for good.

## Before you start

- Git Bash, with the .NET SDK on the PATH (`dotnet --version`).
- You're signed in to GitHub as the repo owner.
- Everything you want to ship is merged into `dev`.

## Steps

Run everything from the repo folder, in Git Bash.

### 1. Update `dev`

```
git checkout dev
git pull
```

The working tree must be clean (`git status` shows nothing to commit).

### 2. Set the new version

Pick the next version number:

- **patch** (`0.2.0` → `0.2.1`): bug fixes only;
- **minor** (`0.2.1` → `0.3.0`): new features;
- **major** (`0.3.0` → `1.0.0`): big changes or breaking ones.

```
tools/bump-version.sh 0.3.0
git push
```

The script writes the number into `src/NameShift.App/NameShift.App.csproj` (`<Version>`), which is where the app and the
release script read it, and commits it on `dev`. It refuses a number that's lower than or equal to the current one, and runs only from `dev`.

Pushing this commit straight to `dev` is the owner-only exception: everyone else changes `dev` through a pull
request.

### 3. Do a dry run

```
tools/release.sh --dry-run
```

It checks everything, builds and runs the tests in Release mode, then shows what it would do, for example:

```
Release v0.3.0: prod 9c1d835 -> 239e26d, and new locked branch releases/v0.3.0 at 239e26d.
Dry run: nothing pushed.
```

### 4. Release

```
tools/release.sh
```

It runs the same checks and tests, asks `Continue? [y/N]`, then pushes `prod` and creates `releases/v0.3.0`. Add
`--yes` to skip the question.

### 5. Check on GitHub

`prod` and `releases/v0.3.0` both point at the same commit as `dev` (the repo's Branches page).

## When the script refuses

| Message | What to do |
|---|---|
| `run it from dev` | `git checkout dev` |
| `the working tree has uncommitted changes` | Commit or stash them first. |
| `dev differs from origin/dev: pull or push first` | `git pull` (or `git push` if you just bumped the version). |
| `releases/vX.Y.Z already exists` | That version is already out: bump the version (step 2). |
| `prod has commits dev doesn't` | Someone changed `prod` by hand. Merge `prod` into `dev` through a pull request, then try again. |
| a build or test error | Fix it on `dev` through a pull request; nothing was pushed. |

## Fixing a bad release

A release branch can't be changed, on purpose. To fix a bad release, fix it on `dev` and publish a new patch
version (`0.3.0` → `0.3.1`).

If `prod` must go back to an older version right away, the owner can move it by hand (only the owner can):

```
git push --force origin releases/v0.2.0:prod
```

Then merge the fix into `dev` and release a new version as usual. `tools/release.sh` refuses to run until `dev`
contains everything that's on `prod`, which is true here because `releases/v0.2.0` is older than `dev`.

## Coming later

The installer (Inno Setup) and GitHub Releases with the downloadable `.exe` aren't in place yet. When they are, the
release script will build the installer and attach it, and this guide will be updated.
