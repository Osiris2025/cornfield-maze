#!/bin/bash
# scripts/verify.sh — Corn Field Maze (does a change really build?)
# Maintained by Lantern (@corn-qa).  Task T2.
#
# THE RULE THIS SCRIPT ENFORCES
#   Unity batchmode exits 0 on a failed build AND on a test run that never
#   started, so an exit code is NOT evidence. This script runs build-mac.sh and
#   then asserts on the ARTEFACT: the log marker line written by
#   CornMazeSetup.cs and the built .app bundle on disk.
#   The build's exit code is printed as context only. It is never an input to
#   the pass/fail decision anywhere in this file.
#
# Output: exactly one final 'PASS verify: ...' or 'FAIL verify: ...' line,
# followed by the evidence it asserted on. Exit 0 on PASS, 1 on FAIL.

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$(cd "$SCRIPT_DIR/.." && pwd)"

LOG="$PROJ/Builds/mac-build.log"
APP="$PROJ/Builds/Corn Field Maze.app"
EXE="$APP/Contents/MacOS/Corn Field Maze"   # note the spaces in the app name — always quote
MARKER='Built standalone player:'

# (a) Record a start marker. We do NOT delete or pre-create the log — a start
#     timestamp is enough to reject a stale log left over from an earlier run.
START_TS="$(date +%s)"

# (b) Run the build. The exit code is captured only to be printed as context.
"$SCRIPT_DIR/build-mac.sh"
BUILD_RC=$?
echo "verify: build-mac.sh exit code = $BUILD_RC  (context only — never used to decide)"

# (c) The log must exist and must have been written by THIS run.
if [ ! -f "$LOG" ]; then
  echo "FAIL verify: build log missing: $LOG"
  exit 1
fi
LOG_MTIME="$(stat -f %m "$LOG")"
if [ "$LOG_MTIME" -lt "$START_TS" ]; then
  echo "FAIL verify: build log is STALE (mtime $LOG_MTIME < start marker $START_TS) — a stale log is not evidence of a new build:"
  echo "  $LOG"
  exit 1
fi

# (d) The log must contain the success marker the editor prints on BuildResult.Succeeded.
#     grep -a: the log is a big text file, treat it as text even if bytes look binary.
MARKER_LINE="$(grep -a -m 1 -F "$MARKER" "$LOG" || true)"
if [ -z "$MARKER_LINE" ]; then
  echo "FAIL verify: no success marker '$MARKER' in $LOG"
  exit 1
fi

# (e) The .app bundle must exist AND be non-empty — test the executable inside
#     Contents/MacOS, not just the directory.
if [ ! -d "$APP" ]; then
  echo "FAIL verify: app bundle missing: $APP"
  exit 1
fi
if [ ! -f "$EXE" ]; then
  echo "FAIL verify: app executable missing: $EXE"
  exit 1
fi
if [ ! -x "$EXE" ]; then
  echo "FAIL verify: app executable is not executable: $EXE"
  exit 1
fi
EXE_SIZE="$(stat -f %z "$EXE")"
if [ "$EXE_SIZE" -le 0 ]; then
  echo "FAIL verify: app executable is empty (0 bytes): $EXE"
  exit 1
fi

# (f) Compile errors are failures even if a marker somehow appeared.
#     'error CS' is checked case-sensitively; 'build failed:' is the editor's own
#     failure signal (CornMazeSetup logs 'Standalone macOS build failed: <result>').
#     Do NOT grep a bare 'failed:' — Unity routinely logs warnings such as
#     '[W] opendir() failed: ...' on a perfectly successful build, which would
#     fail every run.
ERROR_CS="$(grep -a -m 3 'error CS' "$LOG" || true)"
if [ -n "$ERROR_CS" ]; then
  echo "FAIL verify: compile errors ('error CS') in $LOG:"
  echo "$ERROR_CS"
  exit 1
fi
FAILED_LINE="$(grep -a -m 1 'build failed:' "$LOG" || true)"
if [ -n "$FAILED_LINE" ]; then
  echo "FAIL verify: Unity logged a failure line:"
  echo "  $FAILED_LINE"
  exit 1
fi

echo "PASS verify: marker line found and app executable on disk is non-empty"
echo "  marker    : $MARKER_LINE"
echo "  exe size  : $EXE_SIZE bytes"
ls -l "$EXE"
exit 0
