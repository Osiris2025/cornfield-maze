#!/bin/bash
# shoot.sh -- run the built app for a frame capture without putting a window in Todd's face.
#
# Standing rule (Ernie, 2026-09-25): "STOP PUTTING WINDOWS ON TODD'S SCREEN... Shoot frames with the window
# positioned off-screen or minimised, batched rather than one window per frame, and never leave one up."
#
# Frame capture runs the built app, and ScreenCapture needs a window, so a window is unavoidable -- what is
# avoidable is a full-screen one sitting there while he works. This does three things:
#   1. one launch per run (the harness shoots every frame it needs inside that single run),
#   2. shrinks the window to 420x260 and parks it in the bottom-right corner as soon as it appears,
#   3. kills the app the moment the report file lands, so nothing is left on screen.
#
# Movement is best-effort: macOS may refuse to move a window fully off-screen, and a refused move is
# reported rather than silently ignored. The app is always killed.
#
# usage: shoot.sh <path-to-.app> <harness-flag> <report-filename> [timeout-seconds]
set -u

APP="$1"
FLAG="$2"
REPORT_NAME="$3"
TIMEOUT="${4:-300}"
SUPPORT="$HOME/Library/Application Support/arl480/Corn Field Maze"
REPORT="$SUPPORT/$REPORT_NAME"

pkill -f "Corn Field Maze.app/Contents/MacOS" 2>/dev/null
sleep 1

# A report from a previous run is not evidence. Record the old timestamp and require the new file to be newer,
# which is the same stale-artifact trap M29 hit: a poll that finds yesterday's report reports success for a run
# that never happened.
BEFORE=0
[ -f "$REPORT" ] && BEFORE=$(stat -f %m "$REPORT" 2>/dev/null || echo 0)

echo "shoot: launching for $FLAG (one window, cornered, killed as soon as a FRESH $REPORT_NAME lands)"
open -n "$APP" --args "$FLAG" -ApplePersistenceIgnoreState YES -screen-fullscreen 0

# Park the window out of the way as soon as it exists.
sleep 6
osascript <<'EOS' 2>&1 | sed 's/^/shoot: window /'
tell application "System Events"
  if exists (process "Corn Field Maze") then
    tell process "Corn Field Maze"
      try
        set position of window 1 to {1200, 900}
        set size of window 1 to {420, 260}
      end try
      try
        set value of attribute "AXMinimized" of window 1 to true
      end try
    end tell
    return "cornered and minimised"
  else
    return "no process yet"
  end if
end tell
EOS

WAITED=0
FRESH=0
while [ "$WAITED" -lt "$TIMEOUT" ]; do
  if [ -f "$REPORT" ]; then
    NOW=$(stat -f %m "$REPORT" 2>/dev/null || echo 0)
    if [ "$NOW" -gt "$BEFORE" ]; then
      FRESH=1
      sleep 3
      break
    fi
  fi
  sleep 5
  WAITED=$((WAITED + 5))
done

pkill -f "Corn Field Maze.app/Contents/MacOS" 2>/dev/null
sleep 1

if [ "$FRESH" = "1" ]; then
  echo "shoot: fresh report landed after ~${WAITED}s — app closed, window gone"
  ls -l "$REPORT" | awk '{print "shoot: report bytes:", $5}'
else
  echo "shoot: NO FRESH REPORT after ${TIMEOUT}s (a stale one from an earlier run is not evidence) — app closed anyway"
  exit 1
fi
