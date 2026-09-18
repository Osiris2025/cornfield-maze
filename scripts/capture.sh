#!/bin/bash
# scripts/capture.sh — Corn Field Maze (review frame of the RUNNING Mac game)
# Maintained by Lantern (@corn-qa).  Task T3.
#
# Usage: scripts/capture.sh <category> [name]
#   category : cookie | world | audio | defects
#   name     : defaults to a timestamp
# Output:
#   artifacts/review/<category>/<name>.png       (final frame at 2556x1179)
#   artifacts/review/<category>/<name>-raw.png   (untouched screencapture)
#
# RULES THIS SCRIPT OBEYS
#   - The game window rect is READ FRESH from System Events on every run. It is
#     never hardcoded and never reused between runs.
#   - Only the game window may be captured. A whole-screen shot is never a
#     silent fallback; a chat window on top of the game is a failed capture, so
#     the app is activated first and the fresh rect is used.
#   - If the window never appears, this exits 1 rather than capturing anything.

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$(cd "$SCRIPT_DIR/.." && pwd)"

CATEGORY="${1:-}"
NAME="${2:-$(date +%Y%m%d-%H%M%S)}"

case "$CATEGORY" in
  cookie|world|audio|defects) ;;
  *)
    echo "FAIL capture: category must be one of cookie|world|audio|defects (got '${CATEGORY:-<none>}')"
    exit 1
    ;;
esac

APP="$PROJ/Builds/Corn Field Maze.app"     # note the spaces — always quote
PROCESS_NAME="Corn Field Maze"             # the Unity player's process is named after the app
OUTDIR="$PROJ/artifacts/review/$CATEGORY"
FINAL="$OUTDIR/$NAME.png"
RAW="$OUTDIR/$NAME-raw.png"

WANT_W=2556
WANT_H=1179

if [ ! -d "$APP" ]; then
  echo "FAIL capture: app bundle missing: $APP (run scripts/build-mac.sh first)"
  exit 1
fi

mkdir -p "$OUTDIR"

# -ApplePersistenceIgnoreState YES stops Unity restoring a stale window state
# from the previous run, so the rect we read below is the one on screen now.
open -n -a "$APP" --args -ApplePersistenceIgnoreState YES

# --- Read the window rect FRESH, retrying while the app comes up -------------
RECT=""
DEADLINE=$(( $(date +%s) + 30 ))
while [ "$(date +%s)" -lt "$DEADLINE" ]; do
  RECT="$(osascript -e "tell application \"System Events\" to tell process \"$PROCESS_NAME\" to get {position, size} of window 1" 2>/dev/null || true)"
  if [ -n "$RECT" ]; then
    break
  fi
  sleep 1
done

if [ -z "$RECT" ]; then
  echo "FAIL capture: process/window 1 '$PROCESS_NAME' did not appear within 30s — nothing captured"
  exit 1
fi

# System Events returns "x, y, w, h" in screen points — same coordinate space and
# top-left origin as 'screencapture -R'.
X="$(echo "$RECT" | awk -F', ' '{print $1}')"
Y="$(echo "$RECT" | awk -F', ' '{print $2}')"
W="$(echo "$RECT" | awk -F', ' '{print $3}')"
H="$(echo "$RECT" | awk -F', ' '{print $4}')"

if [ -z "$X" ] || [ -z "$Y" ] || [ -z "$W" ] || [ -z "$H" ]; then
  echo "FAIL capture: could not parse window rect from System Events output: '$RECT'"
  exit 1
fi
echo "capture: fresh window rect (x,y,w,h) = $X,$Y,$W,$H"

# Put the game in front so the capture is the game and not whatever is on top.
osascript -e "tell application \"$PROCESS_NAME\" to activate" 2>/dev/null || true
osascript -e "tell application \"System Events\" to tell process \"$PROCESS_NAME\" to set frontmost to true" 2>/dev/null || true
sleep 1

screencapture -x -R"$X,$Y,$W,$H" "$RAW"
if [ ! -f "$RAW" ]; then
  echo "FAIL capture: screencapture produced no file at $RAW (screen-recording permission?)"
  exit 1
fi
echo "capture: raw frame written: $RAW"

if ! command -v sips >/dev/null 2>&1; then
  echo "FAIL capture: 'sips' is not available — cannot crop/scale to ${WANT_W}x${WANT_H}; raw frame is at $RAW"
  exit 1
fi

# Crop to iPhone landscape aspect (2556:1179) then scale to exact pixels.
# sips -c takes HEIGHT then WIDTH and crops from the centre; sips -z takes HEIGHT then WIDTH.
CROP="$(awk -v w="$W" -v h="$H" -v tw="$WANT_W" -v th="$WANT_H" 'BEGIN{
  r = tw / th;
  cw = w; ch = h;
  if (w / h > r) { cw = h * r } else { ch = w / r }
  printf "%d %d", cw, ch
}')"
CROP_W="${CROP%% *}"
CROP_H="${CROP##* }"

sips -c "$CROP_H" "$CROP_W" "$RAW" --out "$FINAL" >/dev/null
sips -z "$WANT_H" "$WANT_W" "$FINAL" >/dev/null

if [ ! -f "$FINAL" ]; then
  echo "FAIL capture: sips did not produce the final frame at $FINAL"
  exit 1
fi

# Evidence: the real pixel size read back off disk.
DIMS="$(sips -g pixelWidth -g pixelHeight "$FINAL" 2>/dev/null || true)"
PW="$(echo "$DIMS" | awk '/pixelWidth/{print $2}')"
PH="$(echo "$DIMS" | awk '/pixelHeight/{print $2}')"

if [ "$PW" != "$WANT_W" ] || [ "$PH" != "$WANT_H" ]; then
  echo "FAIL capture: final frame is ${PW:-?}x${PH:-?}, expected ${WANT_W}x${WANT_H} — resize did not do what we asked"
  echo "  final frame: $FINAL"
  echo "  raw frame  : $RAW"
  sips -g pixelWidth -g pixelHeight "$FINAL" 2>/dev/null || true
  exit 1
fi

echo "PASS capture[$CATEGORY]: $FINAL is ${PW}x${PH} (raw kept at $RAW)"
sips -g pixelWidth -g pixelHeight "$FINAL"
exit 0
