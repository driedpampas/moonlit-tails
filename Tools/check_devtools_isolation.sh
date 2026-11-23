#!/usr/bin/env bash
# Proves the game compiles without DevTools (Assets/MoonlitTails/DevTools): nothing in the game may depend on it.
#
# Makes a copy-on-write copy of Unity/ next to the repo (btrfs/XFS reflink, so it costs almost no disk space),
# deletes DevTools in the copy, batch-compiles the copy, then removes it. Your working copy is never touched.
# It refuses to make a full copy (the Library alone is ~9 GB): on a filesystem without reflinks it stops.
#
# Usage: Tools/check_devtools_isolation.sh   (takes ~2 min; the Editor can stay open, since the copy is a separate project)
# Override the editor with UNITY=/path/to/Unity. Exit code 0 = pass.
set -euo pipefail

repo=$(git -C "$(dirname "$0")" rev-parse --show-toplevel)
unity=${UNITY:-$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Unity}
iso=$(mktemp -d "$(dirname "$repo")/.moonlit-isolation.XXXXXX")
trap 'rm -rf "$iso"' EXIT

echo "Copying Unity/ (copy-on-write) to $iso ..."
if ! cp -a --reflink=always "$repo/Unity" "$iso/Unity" 2>"$iso/cp.err"; then
    echo "FAIL: copy-on-write copy not possible here ($(head -1 "$iso/cp.err")). Not making a full copy." >&2
    exit 2
fi
rm -rf "$iso/Unity/Assets/MoonlitTails/DevTools" "$iso/Unity/Assets/MoonlitTails/DevTools.meta" "$iso/Unity/Temp"

log="$iso/compile.log"
echo "Compiling without DevTools ..."
"$unity" -batchmode -nographics -quit -projectPath "$iso/Unity" -logFile "$log" >/dev/null 2>&1 || true

if grep -qE 'error CS[0-9]+' "$log"; then
    echo "FAIL: the game doesn't compile without DevTools:" >&2
    grep -E 'error CS[0-9]+' "$log" | sort -u >&2
    exit 1
fi
if ! grep -q 'MoonlitTails.Runtime.dll' "$log"; then
    echo "FAIL: couldn't confirm a compile (no MoonlitTails.Runtime.dll in the log). Last lines:" >&2
    tail -20 "$log" >&2
    exit 1
fi
if [ -d "$iso/Unity/Library/ScriptAssemblies" ] && ls "$iso/Unity/Library/ScriptAssemblies" | grep -q 'MoonlitTails.DevTools'; then
    echo "FAIL: DevTools assemblies are still being built in the copy." >&2
    exit 1
fi
echo "PASS: the game compiles without DevTools."
