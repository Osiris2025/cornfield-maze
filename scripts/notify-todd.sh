#!/bin/bash
# notify-todd.sh -- put a short message on Todd's phone.
# Usage: notify-todd.sh "message text"
# Only for things that genuinely need a human. No spam.
#
# `hermes send --to photon` reuses the gateway's Photon credentials, needs no running
# gateway, and returns a message id on success -- so a failure is a failure and not a
# silent no-op. Never put a redacted/masked value in a delivery path: redact the LOGS,
# not the recipient. A masked number reports success and arrives nowhere.
set -u

MSG="${1:-}"
if [ -z "$MSG" ]; then echo "usage: notify-todd.sh \"message\"" >&2; exit 2; fi

OUT="$(hermes send --to photon --json "$MSG" 2>&1)"
RC=$?

if [ "$RC" -eq 0 ] && printf '%s' "$OUT" | grep -q '"success": *true'; then
  echo "notify: sent via photon"
  exit 0
fi

# One retry, then fail LOUDLY.
sleep 3
OUT2="$(hermes send --to photon --json "$MSG" 2>&1)"
if printf '%s' "$OUT2" | grep -q '"success": *true'; then
  echo "notify: sent via photon on retry"
  exit 0
fi

echo "notify: FAILED to reach photon -- Todd has NOT been told" >&2
echo "$OUT" >&2
exit 1
