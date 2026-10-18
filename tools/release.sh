#!/usr/bin/env bash
# Releases what is on origin/dev: fast-forwards prod to it, then creates the locked branch releases/v<version>.
# The version is the <Version> in src/NameShift.App/NameShift.App.csproj; set it first with tools/bump-version.sh.
# Only the repo owner can do this: GitHub refuses other pushes to prod and releases/**.
# Usage: tools/release.sh [--dry-run] [--yes]
set -euo pipefail
cd "$(dirname "$0")/.."

die() { echo "release: $*" >&2; exit 1; }

dry_run=false
assume_yes=false
for arg in "$@"; do
    case "$arg" in
        --dry-run) dry_run=true ;;
        --yes) assume_yes=true ;;
        *) die "usage: tools/release.sh [--dry-run] [--yes]" ;;
    esac
done

[[ "$(git branch --show-current)" == dev ]] || die "run it from dev"
[[ -z "$(git status --porcelain)" ]] || die "the working tree has uncommitted changes"
git fetch --quiet --prune origin
[[ "$(git rev-parse dev)" == "$(git rev-parse origin/dev)" ]] || die "dev differs from origin/dev: pull or push first"

version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/NameShift.App/NameShift.App.csproj)
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "no valid <Version> in the app's csproj (found '$version')"
release_branch="releases/v$version"
if git ls-remote --exit-code --heads origin "$release_branch" >/dev/null; then
    die "$release_branch already exists: bump the version first (tools/bump-version.sh)"
fi
git rev-parse --verify --quiet origin/prod >/dev/null || die "origin/prod doesn't exist"
git merge-base --is-ancestor origin/prod origin/dev || die "prod has commits dev doesn't: it can't be fast-forwarded"

echo "Building and testing (Release)..."
dotnet build --configuration Release --verbosity quiet --nologo
dotnet test --configuration Release --no-build --verbosity quiet --nologo

commit=$(git rev-parse --short origin/dev)
echo
echo "Release v$version: prod $(git rev-parse --short origin/prod) -> $commit, and new locked branch $release_branch at $commit."
if $dry_run; then
    echo "Dry run: nothing pushed."
    exit 0
fi
if ! $assume_yes; then
    read -rp "Continue? [y/N] " answer
    [[ "$answer" == [yY] ]] || die "cancelled"
fi

git push origin "origin/dev:refs/heads/prod"
git push origin "origin/dev:refs/heads/$release_branch"
git fetch --quiet origin
git branch --force prod origin/prod >/dev/null
echo "Released v$version: prod and $release_branch are at $commit."
