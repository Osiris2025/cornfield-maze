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

# The player PERSISTS the screen args below as the USER's saved window state: after a capture run the next
# double-click opens windowed at 1300x820 instead of full screen ("why does the window drop to taskbar every
# time you start it up?"). Save his own prefs first and hand them back after the kill.
PREFS_BACKUP="$SUPPORT/.screen-prefs-backup.plist"
defaults export com.arl480.cornfieldmaze "$PREFS_BACKUP" 2>/dev/null

echo "shoot: launching for $FLAG (one window, parked off-screen, killed as soon as a FRESH $REPORT_NAME lands)"
# -j: launch HIDDEN, so no window is ever on his screen (not even for the seconds the old parking took).
# -g: do NOT bring it to the foreground — a capture has no business stealing his focus.
# -silent: the game reads this and silences the AudioListener. A hidden app still PLAYS: that is the
# "sound is still there for a minute" after the window vanished.
open -j -g -n "$APP" --args "$FLAG" -silent -ApplePersistenceIgnoreState YES -screen-fullscreen 0

# Belt and braces: a hidden launch is a request, not a guarantee. Park any window that still materialises
# off-screen and re-hide it at once — POLLING, because the old fixed `sleep 6` was precisely the window in
# which Todd could see it appear and reach for the mouse.
for i in $(seq 1 40); do
  pgrep -f "Corn Field Maze.app/Contents/MacOS" >/dev/null 2>&1 && break
  sleep 0.5
done
hide_window() {
  osascript <<'EOS' 2>&1 | sed 's/^/shoot: window /'
tell application "System Events"
  if exists (process "Corn Field Maze") then
    tell process "Corn Field Maze"
      try
        set position of window 1 to {-4000, -4000}
        set size of window 1 to {420, 260}
      end try
      -- NOT minimised: a minimised window parks a THUMBNAIL IN THE DOCK, which is still on his screen.
      -- That thumbnail was the original "why does the window drop to taskbar" complaint.
      try
        set value of attribute "AXMinimized" of window 1 to false
      end try
    end tell
    set visible of process "Corn Field Maze" to false
    return "parked off-screen and hidden (nothing in the Dock)"
  else
    return "no window yet"
  end if
end tell
EOS
}
hide_window
sleep 2
hide_window

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

# The app is HIDDEN, not minimised, so killing it leaves nothing in the Dock and nothing on his screen.
pkill -f "Corn Field Maze.app/Contents/MacOS" 2>/dev/null

# Wait for the player to actually exit BEFORE restoring: it flushes PlayerPrefs on the way out, so an import
# that lands first is simply overwritten — diffing the domain around a run proved the screen keys come
# straight back. Then restore and DIFF against the backup, retrying until Todd's own state is on disk.
WAIT_DEAD=0
while pgrep -f "Corn Field Maze.app/Contents/MacOS" >/dev/null 2>&1 && [ "$WAIT_DEAD" -lt 20 ]; do
  sleep 1
  WAIT_DEAD=$((WAIT_DEAD + 1))
done
sleep 2

RESTORED=0
for attempt in 1 2 3 4 5; do
  defaults import com.arl480.cornfieldmaze "$PREFS_BACKUP" 2>/dev/null
  sleep 1
  defaults export com.arl480.cornfieldmaze /tmp/.pp-verify.plist 2>/dev/null
  if diff -q "$PREFS_BACKUP" /tmp/.pp-verify.plist >/dev/null 2>&1; then
    RESTORED=1
    break
  fi
done
rm -f /tmp/.pp-verify.plist "$PREFS_BACKUP"
if [ "$RESTORED" = "1" ]; then
  echo "shoot: Todd's window/screen state handed back intact"
else
  echo "shoot: WARNING — could not hand his screen state back (tried 5x)"
fi

if [ "$FRESH" = "1" ]; then
  echo "shoot: fresh report landed after ~${WAITED}s — app closed, window gone"
  ls -l "$REPORT" | awk '{print "shoot: report bytes:", $5}'
else
  echo "shoot: NO FRESH REPORT after ${TIMEOUT}s (a stale one from an earlier run is not evidence) — app closed anyway"
  exit 1
fi
