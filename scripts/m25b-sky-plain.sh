#!/usr/bin/env bash
# M25b evidence: the moon in the sky from a PLAIN launch — no flags, no harness, the way Todd runs it.
#
# Why this exists instead of calling capture.sh twice: capture.sh takes ONE frame the moment the window
# appears. On a cold start that frame is the Unity splash (the M25b smoke test caught "Made with Unity"),
# and calling it twice puts TWO processes called "Corn Field Maze" on the machine — which is exactly what
# made the window lookup fail with System Events error -1719 in earlier passes. So: one launch, wait for
# the world, press SPACE like a player would (GameFrontEnd advances on Space/Return), shoot t0, then wait
# out §25.5's 40-second rise on the SAME instance and shoot t40.
#
#   scripts/m25b-sky-plain.sh            -> artifacts/review/world/m25b-sky-t0.png and -t40.png
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP="${APP:-$ROOT/Builds/Corn Field Maze.app}"
OUT="${OUT:-$ROOT/artifacts/review/world}"
NAME="${NAME:-m25b-sky}"
SETTLE="${SETTLE:-6}"     # seconds after the window appears: splash + world load
RISE="${RISE:-40}"        # §25.5: the moon crosses the horizon in the first 40 s of play
WANT_W=2556
WANT_H=1179
PROCESS_NAME="Corn Field Maze"

mkdir -p "$OUT"

window_rect() {
  osascript -e "tell application \"System Events\" to tell process \"$PROCESS_NAME\" to get {position, size} of window 1" 2>/dev/null || true
}

shoot() {   # $1 = suffix
  local suffix="$1"
  local raw="$OUT/$NAME-$suffix-raw.png"
  local final="$OUT/$NAME-$suffix.png"
  local rect x y w h

  rect="$(window_rect)"
  if [ -z "$rect" ]; then
    echo "FAIL capture[$suffix]: no window for '$PROCESS_NAME' — nothing captured"
    exit 1
  fi
  x="$(echo "$rect" | awk -F', ' '{print $1}')"
  y="$(echo "$rect" | awk -F', ' '{print $2}')"
  w="$(echo "$rect" | awk -F', ' '{print $3}')"
  h="$(echo "$rect" | awk -F', ' '{print $4}')"
  if [ -z "$x" ] || [ -z "$y" ] || [ -z "$w" ] || [ -z "$h" ]; then
    echo "FAIL capture[$suffix]: could not parse the window rect '$rect'"
    exit 1
  fi

  osascript -e "tell application \"$PROCESS_NAME\" to activate" 2>/dev/null || true
  osascript -e "tell application \"System Events\" to tell process \"$PROCESS_NAME\" to set frontmost to true" 2>/dev/null || true
  sleep 1

  screencapture -x -R"$x,$y,$w,$h" "$raw"
  [ -f "$raw" ] || { echo "FAIL capture[$suffix]: screencapture wrote nothing (screen-recording permission?)"; exit 1; }

  # Same crop/scale as capture.sh: centred crop to the iPhone landscape aspect, then exact pixels.
  local crop cw ch pw ph
  crop="$(awk -v w="$w" -v h="$h" -v tw="$WANT_W" -v th="$WANT_H" 'BEGIN{
    r = tw / th; cw = w; ch = h;
    if (w / h > r) { cw = h * r } else { ch = w / r }
    printf "%d %d", cw, ch }')"
  cw="${crop%% *}"; ch="${crop##* }"
  sips -c "$ch" "$cw" "$raw" --out "$final" >/dev/null
  sips -z "$WANT_H" "$WANT_W" "$final" >/dev/null

  pw="$(sips -g pixelWidth "$final" | awk '/pixelWidth/{print $2}')"
  ph="$(sips -g pixelHeight "$final" | awk '/pixelHeight/{print $2}')"
  if [ "$pw" != "$WANT_W" ] || [ "$ph" != "$WANT_H" ]; then
    echo "FAIL capture[$suffix]: frame is ${pw:-?}x${ph:-?}, expected ${WANT_W}x${WANT_H}"
    exit 1
  fi
  echo "PASS capture[$suffix]: $final is ${pw}x${ph} (raw $raw, window ${w}x${h} at $x,$y)"
}

echo "launching $APP (plain, no flags)"

# One instance only. capture.sh leaves its app running, and a second process with the same name makes the
# System Events window lookup fail (Invalid index -1719) — which is what silently broke window capture for
# three earlier passes. Clear the field first and say so.
if pgrep -f "Builds/Corn Field Maze.app" >/dev/null 2>&1; then   # why: stale instance breaks window lookup
  echo "note: an instance of the game is already running — shutting it down (one instance only)"
  pkill -f "Builds/Corn Field Maze.app" || true
  sleep 2
fi

open -n -a "$APP" --args -ApplePersistenceIgnoreState YES

# Wait for the window to exist at all.
rect=""
for _ in $(seq 1 40); do
  rect="$(window_rect)"
  [ -n "$rect" ] && break
  sleep 1
done
[ -n "$rect" ] || { echo "FAIL: the game never opened a window"; exit 1; }

sleep "$SETTLE"

# Start the run the way a player does. This is the only input this script sends.
# SPACE advances Title -> 4 intro cards -> Playing (GameFrontEnd.Update: 1 press to leave the title, then one
# per card, then the press that calls BeginRun). The first run's "t40" frame was intro card 0 and the second
# attempt's was card 2, which is why this presses until the intro is exhausted. A press while Playing does
# nothing, and mouse look only goes live once Playing locks the cursor, so the presses come before the aim.
osascript -e "tell application \"$PROCESS_NAME\" to activate" 2>/dev/null || true
for _press in 1 2 3 4 5 6; do
  osascript -e "tell application \"System Events\" to tell process \"$PROCESS_NAME\" to keystroke \" \"" 2>/dev/null || true
  sleep 2
done
PLAY_START="$(date +%s)"
echo "started play at $(date '+%H:%M:%S') (6 SPACE presses: title + 4 intro cards + 1 no-op)"

# Aim at the moon. §25.5 puts it 106 deg off the camera's yaw and, by the end of the rise, 54 deg above a
# camera that starts pitched 27 deg DOWN at the lane — so a plain launch frames the corn, not the moon, and
# a frame without the moon in it is not evidence about the moon. Turn the view with a mouse drag, which is
# how the Mac build's look control works (FarmWalkerController: Mouse X -> yaw, Mouse Y -> pitch, 2.1 deg
# per axis unit). Two drags, each starting from the window centre so the whole drag stays in the window.
rect="$(window_rect)"
WX="$(echo "$rect" | awk -F', ' '{print $1}')"
WY="$(echo "$rect" | awk -F', ' '{print $2}')"
WW="$(echo "$rect" | awk -F', ' '{print $3}')"
WH="$(echo "$rect" | awk -F', ' '{print $4}')"
CX=$(( WX + WW / 2 ))
CY=$(( WY + WH / 2 ))
if [ "${AIM:-1}" = "1" ]; then
  echo "aiming at the moon: yaw drag +${AIM_X:-505}px, pitch drag +${AIM_Y:-256}px, from window centre $CX,$CY"
  osascript -l JavaScript "$ROOT/scripts/m25b_mouse_drag.js" "${AIM_X:-505}" 0 "$CX" "$CY" || echo "aim: yaw drag failed"
  osascript -l JavaScript "$ROOT/scripts/m25b_mouse_drag.js" 0 "${AIM_Y:-256}" "$CX" "$CY" || echo "aim: pitch drag failed"
fi

sleep 2
shoot t0

# §25.5's night rule is "40 s OR 2 cells in, whichever comes first", so walking two cells brings full night
# early. Walk W for three seconds (~2 cells at the walk speed), then stand still: the Husk catches a
# motionless player every time, which is the design, and it is what makes t~40 s a death frame if nothing is
# done about it. The sky does NOT reset on a death — DuskSky starts its clock once — so the honest way to a
# clean night frame at t~40 s is to die, press R (which revives the player AND puts the camera back at its
# spawn orientation, so the aim below is valid again), and re-aim.
osascript -l JavaScript "$ROOT/scripts/m25b_key_hold.js" 13 3 >/dev/null || echo "walk: key hold failed"

NOW="$(date +%s)"
REMAIN=$(( RISE - 6 - (NOW - PLAY_START) ))
[ "$REMAIN" -gt 0 ] && sleep "$REMAIN"

echo "reviving (R) before the night frame at $(date '+%H:%M:%S') — the sky clock keeps running"
osascript -e "tell application \"$PROCESS_NAME\" to activate" 2>/dev/null || true
osascript -e "tell application \"System Events\" to tell process \"$PROCESS_NAME\" to keystroke \"r\"" 2>/dev/null || true
sleep 1
if [ "${AIM:-1}" = "1" ]; then
  osascript -l JavaScript "$ROOT/scripts/m25b_mouse_drag.js" "${AIM_X:-505}" 0 "$CX" "$CY" || echo "aim: yaw drag failed"
  osascript -l JavaScript "$ROOT/scripts/m25b_mouse_drag.js" 0 "${AIM_Y:-256}" "$CX" "$CY" || echo "aim: pitch drag failed"
fi
sleep 1
shoot t40

osascript -e "tell application \"$PROCESS_NAME\" to quit" 2>/dev/null || true
echo "plain-launch frames done: $OUT/$NAME-t0.png , $OUT/$NAME-t40.png"
