#!/usr/bin/env bash
# Builds the installer: installer/Output/NameShift-Setup-<version>.exe (Inno Setup 6, per user).
# Run it in Git Bash after tools/release.sh; it changes no tracked file (installer/Output/ is ignored).
# Usage: installer/build-installer.sh
set -euo pipefail
cd "$(dirname "$0")/.."

die() { echo "build-installer: $*" >&2; exit 1; }

csproj=src/NameShift.App/NameShift.App.csproj
app_bin=src/NameShift.App/bin/Release/net48
cmd_bin=src/NameShift.Cmd.Exe/bin/Release/net48
out=installer/Output
stage=$out/stage
log=$out/build.log
max_bytes=3145728
required=(NameShift.exe NameShiftCmd.exe NameShift.exe.config NameShiftCmd.exe.config
          NameShift.Core.dll NameShift.Disk.dll NameShift.Ui.dll NameShift.Cmd.dll)

# 1. Build.
echo "Building (Release)..."
dotnet build -c Release --nologo

# 2. Version: the csproj <Version> (set by tools/bump-version.sh) must equal the built NameShift.exe's file version.
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$csproj")
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "no valid <Version> in $csproj (found '$version')"
[[ -f "$app_bin/NameShift.exe" ]] || die "$app_bin/NameShift.exe is missing after the build"
exe_win=$(cygpath -wa "$app_bin/NameShift.exe")
file_version=$(powershell -NoProfile -Command "(Get-Item -LiteralPath '$exe_win').VersionInfo.FileVersion" | tr -d '\r')
exe_version=$(echo "$file_version" | cut -d. -f1-3)
[[ "$exe_version" == "$version" ]] ||
    die "NameShift.exe has file version '$file_version' but $csproj says '$version'"
echo "Version $version"

# 3. Stage the Release output of both programs (without .pdb and .xml), then LICENSE and README.md.
rm -rf "$stage"
mkdir -p "$stage"
for bin in "$app_bin" "$cmd_bin"; do
    [[ -d "$bin" ]] || die "$bin is missing after the build"
    while IFS= read -r -d '' file; do
        relative=${file#"$bin"/}
        target=$stage/$relative
        if [[ -e "$target" ]]; then
            # The same library in both outputs is installed once; it must be the same build.
            cmp -s "$file" "$target" || die "$relative differs between $app_bin and $cmd_bin"
            continue
        fi
        mkdir -p "$(dirname "$target")"
        cp "$file" "$target"
    done < <(find "$bin" -type f ! -name '*.pdb' ! -name '*.xml' -print0)
done
cp LICENSE README.md "$stage/"

# 4. Every file the programs need is there.
for name in "${required[@]}" LICENSE README.md; do
    [[ -f "$stage/$name" ]] || die "$name is missing from $stage"
done

# 5. Find ISCC.exe: the PATH, then the standard Inno Setup 6 folders.
iscc_folders=()
[[ -n "${LOCALAPPDATA:-}" ]] && iscc_folders+=("$(cygpath -u "$LOCALAPPDATA")/Programs/Inno Setup 6")
pf86=$(printenv 'ProgramFiles(x86)' || true)
[[ -n "$pf86" ]] && iscc_folders+=("$(cygpath -u "$pf86")/Inno Setup 6")
[[ -n "${PROGRAMFILES:-}" ]] && iscc_folders+=("$(cygpath -u "$PROGRAMFILES")/Inno Setup 6")
iscc=$(command -v iscc || command -v ISCC.exe || true)
if [[ -z "$iscc" ]]; then
    for folder in "${iscc_folders[@]}"; do
        if [[ -f "$folder/ISCC.exe" ]]; then
            iscc=$folder/ISCC.exe
            break
        fi
    done
fi
if [[ -z "$iscc" ]]; then
    echo "build-installer: ISCC.exe (Inno Setup 6) was not found on the PATH nor in:" >&2
    for folder in "${iscc_folders[@]}"; do echo "  $folder" >&2; done
    echo "Install Inno Setup 6 from https://jrsoftware.org/isdl.php, then run this script again." >&2
    exit 1
fi
echo "Inno Setup: $iscc"

# 6. Compile. MSYS2_ARG_CONV_EXCL keeps Git Bash from rewriting the /D switches as paths.
setup=$out/NameShift-Setup-$version.exe
rm -f "$setup"
stage_win=$(cygpath -wa "$stage")
iss_win=$(cygpath -wa installer/NameShift.iss)
if ! MSYS2_ARG_CONV_EXCL='*' "$iscc" "/DAppVersion=$version" "/DStageDir=$stage_win" "$iss_win" > "$log" 2>&1; then
    tail -n 20 "$log" >&2
    die "ISCC failed; the full log is $log"
fi

# 7. The setup exists and stays small.
[[ -f "$setup" ]] || die "$setup was not produced (see $log)"
size=$(stat -c %s "$setup")
(( size <= max_bytes )) || die "$setup is $size bytes, over the $max_bytes-byte limit"
echo "Built $setup: $size bytes (limit $max_bytes). Log: $log"
