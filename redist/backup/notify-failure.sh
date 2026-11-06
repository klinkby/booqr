#!/bin/bash
# Called via OnFailure= when booqr-backup.service fails. Posts to ntfy (NTFY_URL in restic.env).
set -uo pipefail

# shellcheck disable=SC1091
. "$(dirname "$0")/restic.env"
: "${NTFY_URL:?NTFY_URL not set in restic.env}"

UNIT="${1:-booqr-backup.service}"
DETAIL="$(journalctl --user -u "$UNIT" -n 8 --no-pager -o cat 2>/dev/null | tail -8)"
# A system user (UID < 1000) has no per-user journal: its logs land in the system journal, which
# it can't read — point at the command instead of sending an empty excerpt.
[ -n "$DETAIL" ] || DETAIL="(not readable as $(id -un)) — on the box: sudo journalctl _UID=$(id -u) _SYSTEMD_USER_UNIT=$UNIT -n 40"

# Retries: the failure may be the network itself coming up late after boot.
curl -fsS --max-time 20 --retry 5 --retry-delay 10 --retry-all-errors \
  -H "Title: booqr backup FAILED on $(hostname)" \
  -H "Priority: high" \
  -H "Tags: warning,floppy_disk" \
  -d "Unit ${UNIT} failed at $(date -Is).

Last log lines:
${DETAIL}" \
  "$NTFY_URL" >/dev/null \
  && echo "failure notification sent" \
  || echo "WARNING: failed to send ntfy notification"
