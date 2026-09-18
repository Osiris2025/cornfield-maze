#!/bin/bash
# scripts/build-mac.sh — Corn Field Maze (Mac standalone player, Unity batchmode)
# Maintained by Lantern (@corn-qa).  Task T2.
#
# WHAT THIS DOES
#   Runs the pinned Unity 6000.3.23f1 editor in batchmode and calls
#   CornMaze.EditorTools.CornMazeSetup.BuildStandaloneMac, which writes
#   'Builds/Corn Field Maze.app'.
#
# THE EXIT CODE IS NOT VERIFICATION
#   Unity batchmode exits 0 even when the build FAILED, so the exit code printed
#   below proves nothing about whether the game was produced. Use scripts/verify.sh,
#   which asserts on the artefact (the .app bundle + the 'Built standalone player:'
#   marker line in the log).
#
# ONE UNITY WRITER AT A TIME
#   Two editors on the same project corrupt Library/. The pre-flight guard below
#   refuses to run if any other Unity process already has this project open.

set -u   # deliberately NOT -e: a failing Unity must not abort before we report.

# Resolve the project root as the parent of this script's own directory, so the
# script behaves the same no matter where the caller's cwd is.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$(cd "$SCRIPT_DIR/.." && pwd)"

# Pinned editor only — never a second install, never another version.
UNITY_BIN="/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity"
BUILD_METHOD="CornMaze.EditorTools.CornMazeSetup.BuildStandaloneMac"
LOG_PATH="$PROJ/Builds/mac-build.log"

if [ ! -d "$PROJ" ]; then
  echo "FAIL build-mac: project path does not exist: $PROJ" >&2
  exit 2
fi

if [ ! -x "$UNITY_BIN" ]; then
  echo "FAIL build-mac: pinned Unity binary missing or not executable: $UNITY_BIN" >&2
  exit 2
fi

# --- Pre-flight guard: one Unity writer at a time ---------------------------
# Match any Unity process whose command line mentions THIS project path,
# excluding this script's own pid.
SELF_PID=$$
OTHER_UNITY="$(pgrep -fl "Unity" 2>/dev/null | grep -F -- "$PROJ" | grep -v -E "^${SELF_PID} " || true)"
if [ -n "$OTHER_UNITY" ]; then
  echo "FAIL build-mac: another Unity process already has this project open." >&2
  echo "Refusing to start a second writer (two editors corrupt Library/)." >&2
  echo "$OTHER_UNITY" >&2
  exit 3
fi

# --- Pre-flight guard: the output volume MUST support hard links ------------
# Unity's macOS build LINKS resources into the .app bundle rather than copying them.
# On exFAT `ln` returns "Operation not supported", so the build first creates the
# destination at ZERO BYTES and then dies:
#     Copying .../unity_builtin_extra failed: Operation not permitted
#     *** Tundra build failed
# On top of that, macOS writes AppleDouble "._*" siblings next to every file Unity
# creates on such a volume, and the linker then tries to load them as managed
# assemblies:
#     ._Assembly-CSharp.dll -> BadImageFormatException -> Burst compiler failed
# Both are properties of the FILESYSTEM, not of the project, so no amount of cleaning
# beforehand wins -- the junk is recreated during the build. Build on APFS instead.
OUT_DIR="${CORN_BUILD_OUT:-$PROJ/Builds}"
mkdir -p "$OUT_DIR"
PROBE="$OUT_DIR/.hardlink_probe_$$"
printf 'x' > "$PROBE.src" 2>/dev/null || true
if ln "$PROBE.src" "$PROBE.link" 2>/dev/null; then
  mv "$PROBE.src" "$PROBE.link" /tmp/ 2>/dev/null || true
else
  mv "$PROBE.src" /tmp/ 2>/dev/null || true
  echo "FAIL build-mac: '$OUT_DIR' does not support hard links (exFAT)." >&2
  echo "Unity's macOS build links resources into the bundle and will fail with" >&2
  echo "'Operation not permitted' / 'Tundra build failed', and on this volume macOS also" >&2
  echo "writes AppleDouble ._* files that the linker reads as assemblies" >&2
  echo "(BadImageFormatException -> Burst failed)." >&2
  echo >&2
  echo "Remedy: build from an APFS working copy, or set CORN_BUILD_OUT to an APFS path." >&2
  echo "  rsync -a --exclude '._*' --exclude Library/ --exclude Builds/ --exclude .git/ \\" >&2
  echo "        \"$PROJ/\" /Users/toddadams/CornMazeWork/CornFieldMaze/" >&2
  exit 4
fi

cd "$PROJ" || exit 2

echo "build-mac: project   $PROJ"
echo "build-mac: log file  $LOG_PATH"

"$UNITY_BIN" -batchmode -projectPath "$PROJ" -executeMethod "$BUILD_METHOD" -logFile "$LOG_PATH" -quit
UNITY_RC=$?

echo "build-mac: Unity exit code = $UNITY_RC"
echo "build-mac: NOTE — this exit code is NOT verification. Unity batchmode exits 0 even on a"
echo "build-mac: failed build. Assert on the artefact with scripts/verify.sh."

exit "$UNITY_RC"
