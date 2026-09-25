#!/bin/bash
# crew-supervisor.sh -- keep the Corn Field Maze crew running until the job is done.
#
# Behaviour:
#   * if a crew run is already in flight, wait for it
#   * when it exits, dispatch the next continuation order
#   * stop ONLY on: done marker, blocked marker, stop file, or repeated hard failures
#   * text Todd ONLY when a human is genuinely required
#
# Markers (written by the crew, never inferred from prose):
#   /tmp/corn-crew-done        -- the WHOLE mission is finished (not one phase)
#   /tmp/corn-crew-blocked     -- contains the one-line reason a human is needed
#   /tmp/corn-supervisor-stop  -- an operator wants the loop to end
#
# NOTE: the supervisor RENAMES a blocked marker to <marker>.sent after texting, so read
# /tmp/corn-crew-blocked.sent before asking Todd the same thing twice.

set -u
REPO="/Volumes/files1/projects/cornmaze/CornFieldMaze"   # fixed 2026-09-23: files2 tree is gone (retired in the files1 move); a stale REPO made `cd || exit 1` kill the supervisor on startup
CHIEF_SESSION="20260917_203359_dcd37b"     # first pass; continuation order resumes it
ORDER="/tmp/corn-order-m29.txt"    # M29 ground wiring, then M27 first person, then M28 the scarecrow.
NOTIFY="$REPO/scripts/notify-todd.sh"
DONE=/tmp/corn-crew-done
BLOCKED=/tmp/corn-crew-blocked
STOPF=/tmp/corn-supervisor-stop
MAX_ITER=8
STALL_MIN=20          # minutes of no log growth before a live process counts as hung
STALL_TOLD=no
WAITING=no            # MUST be initialised: this script runs under set -u, so an unset
                      # $WAITING aborts the loop the instant it first checks for a live crew
FAILS=0
ITER=0

log() { echo "[supervisor $(date '+%H:%M:%S')] $*" | tee -a /tmp/corn-supervisor.log; }
tell() { log "TEXTING TODD: $1"; bash "$NOTIFY" "$1" 2>&1 | sed 's/^/  notify: /'; }

cd "$REPO" || exit 1
log "armed. repo=$REPO. waiting for work. pid=$$"

while true; do
  if [ -f "$STOPF" ]; then
    log "stop file present -- standing down (no text, this was asked for)"; exit 0
  fi

  if [ -f "$DONE" ]; then
    log "DONE marker: $(cat "$DONE" 2>/dev/null)"
    tell "Corn Field Maze: the crew reports the mission finished. $(head -c 220 "$DONE" 2>/dev/null)"
    exit 0
  fi

  if [ -f "$BLOCKED" ]; then
    REASON="$(head -c 400 "$BLOCKED" 2>/dev/null)"
    log "BLOCKED marker: $REASON"
    tell "Corn Field Maze needs you: $REASON"
    mv "$BLOCKED" "${BLOCKED}.sent" 2>/dev/null
    exit 0
  fi

  # A crew run in flight? Wait for it -- but a live process is NOT proof of life.
  # An agent waiting on a model response sits at 0% CPU, so CPU says nothing; a worker
  # that is working grows its log. Match the INTERPRETER PATH, not the bare profile name:
  # "hermes -p corn" also matches any shell whose command line merely MENTIONS it.
  # IMPORTANT: pgrep also matches ZOMBIES. A worker that finished but whose parent has not
  # reaped it still sits in the process table, so a bare pgrep makes the supervisor believe
  # the crew is still running and it waits forever on a dead pass. Exclude state Z.
  CREW="$(ps -eo pid=,state=,args= | awk '$2 !~ /Z/ && /hermes-agent\/hermes -p corn-/ {print $1; exit}')"
  if [ -n "$CREW" ]; then
    if [ "$WAITING" = "no" ]; then
      log "crew pid $CREW is running -- waiting for it to finish"
      WAITING=yes
    fi
    # Exclude the supervisor's own log: its fresh mtime would mask a real stall.
    NEWEST="$(ls -t /tmp/corn-*.log 2>/dev/null | grep -v 'corn-supervisor' | head -1)"
    if [ -n "$NEWEST" ]; then
      AGE=$(( $(date +%s) - $(stat -f %m "$NEWEST" 2>/dev/null || echo 0) ))
      if [ "$AGE" -gt $((STALL_MIN * 60)) ]; then
        if [ "$STALL_TOLD" = "no" ]; then
          log "STALL: $NEWEST untouched for $((AGE / 60)) min while a process is alive"
          tell "Corn Field Maze: a crew process is alive but has written nothing for $((AGE / 60)) minutes, so it looks hung rather than busy. Nothing has stopped and I am still watching. Log: $NEWEST"
          STALL_TOLD=yes
        fi
      else
        STALL_TOLD=no
      fi
    fi
    sleep 60
    continue
  fi
  [ "$WAITING" = "yes" ] && log "crew finished -- no live worker remains"
  WAITING=no

  ITER=$((ITER + 1))
  if [ "$ITER" -gt "$MAX_ITER" ]; then
    log "iteration cap $MAX_ITER reached"
    tell "Corn Field Maze: the crew has run $MAX_ITER back-to-back passes and I am stopping the loop rather than burn more. Last log: /tmp/corn-supervisor.log"
    exit 0
  fi

  log "iter $ITER: dispatching continuation"
  hermes -p corn-chief chat --resume "$CHIEF_SESSION" -q "$(cat "$ORDER")" \
    > "/tmp/corn-supervisor-iter${ITER}.log" 2>&1
  RC=$?
  log "iter $ITER: exit=$RC, log $(wc -c < "/tmp/corn-supervisor-iter${ITER}.log" | tr -d ' ') bytes"

  if [ "$RC" -ne 0 ]; then
    FAILS=$((FAILS + 1))
    log "iter $ITER: nonzero exit ($FAILS consecutive)"
    if [ "$FAILS" -ge 3 ]; then
      tell "Corn Field Maze: three crew dispatches in a row failed to even start (exit $RC). I need you to look. Log: /tmp/corn-supervisor-iter${ITER}.log"
      exit 0
    fi
  else
    FAILS=0
  fi
done
