#!/bin/bash
# scripts/build-ios.sh — Corn Field Maze (iOS Xcode project, Unity batchmode)
# Maintained by Lantern (@corn-qa).  Task T3.
#
# WHAT THIS DOES
#   Runs the pinned Unity 6000.3.23f1 editor in batchmode and calls
#   CornMaze.EditorTools.CornMazeSetup.BuildIosPlayer, which writes the Xcode
#   project 'Builds/iOS/Unity-iPhone.xcodeproj'.
#   The success marker line in the log is: 'Built iOS Xcode project: <path>'
#   (failure logs 'iOS build failed: <result>' via Debug.LogError).
#
# THE EXIT CODE IS NOT VERIFICATION
#   Unity batchmode exits 0 even when the build FAILED. Assert on the artefact
#   (Builds/iOS/Unity-iPhone.xcodeproj on disk + the marker line in Builds/ios-build.log).
#
# M6 DEVICE-BUILD PRECURSOR
#   Signing is NOT done here. appleDeveloperTeamID is empty and automatic signing
#   is off in the generated project, so the archive/device install happens later
#   in Xcode, not in this script. This script only produces the Xcode project.
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
BUILD_METHOD="CornMaze.EditorTools.CornMazeSetup.BuildIosPlayer"
LOG_PATH="$PROJ/Builds/ios-build.log"

if [ ! -d "$PROJ" ]; then
  echo "FAIL build-ios: project path does not exist: $PROJ" >&2
  exit 2
fi

if [ ! -x "$UNITY_BIN" ]; then
  echo "FAIL build-ios: pinned Unity binary missing or not executable: $UNITY_BIN" >&2
  exit 2
fi

# --- Pre-flight guard: one Unity writer at a time ---------------------------
# Match any Unity process whose command line mentions THIS project path,
# excluding this script's own pid.
SELF_PID=$$
OTHER_UNITY="$(pgrep -fl "Unity" 2>/dev/null | grep -F -- "$PROJ" | grep -v -E "^${SELF_PID} " || true)"
if [ -n "$OTHER_UNITY" ]; then
  echo "FAIL build-ios: another Unity process already has this project open." >&2
  echo "Refusing to start a second writer (two editors corrupt Library/)." >&2
  echo "$OTHER_UNITY" >&2
  exit 3
fi

mkdir -p "$PROJ/Builds"
cd "$PROJ" || exit 2

echo "build-ios: project   $PROJ"
echo "build-ios: artefact  Builds/iOS/Unity-iPhone.xcodeproj"
echo "build-ios: log file  $LOG_PATH"

"$UNITY_BIN" -batchmode -projectPath "$PROJ" -executeMethod "$BUILD_METHOD" -logFile "$LOG_PATH" -quit
UNITY_RC=$?

echo "build-ios: Unity exit code = $UNITY_RC"
echo "build-ios: NOTE — this exit code is NOT verification. Unity batchmode exits 0 even on a"
echo "build-ios: failed build. Assert on the artefact (Builds/iOS/Unity-iPhone.xcodeproj)."

exit "$UNITY_RC"
