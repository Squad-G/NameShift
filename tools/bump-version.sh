#!/usr/bin/env bash
# Sets the app version (the <Version> in src/NameShift.App/NameShift.App.csproj) and commits it.
# Usage: tools/bump-version.sh <major.minor.patch>
set -euo pipefail
cd "$(dirname "$0")/.."

die() { echo "bump-version: $*" >&2; exit 1; }

csproj=src/NameShift.App/NameShift.App.csproj
new=${1:-}
[[ "$new" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "usage: tools/bump-version.sh <major.minor.patch>"
[[ -z "$(git status --porcelain)" ]] || die "the working tree has uncommitted changes"

current=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$csproj")
[[ "$new" != "$current" ]] || die "the version is already $current"
highest=$(printf '%s\n%s\n' "$current" "$new" | sort -V | tail -n 1)
[[ "$highest" == "$new" ]] || die "$new is lower than the current version $current"

sed -i "s:<Version>$current</Version>:<Version>$new</Version>:" "$csproj"
git add "$csproj"
git commit --quiet -m "Version $new"
echo "Version $current → $new, committed on $(git branch --show-current). Push it to dev, then run tools/release.sh."
