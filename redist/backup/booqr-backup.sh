#!/bin/bash
# booqr -> off-site backup, restic over SFTP (e.g. a Hetzner Storage Box sub-account).
# Runs as the service user from booqr-backup.timer. Three snapshots per run:
#   db-booqr  pg_dump -Fc of the booqr DB, streamed (no plaintext dump on disk)
#   db-roles  pg_dumpall --roles-only (booqr_* and per-tenant t_<id> roles live at cluster level)
#   config    the project dir incl. env/*.env (holds the tenant master secret — every tenant
#             role password is derived from it, so a DB restore is useless without it)
# Excluded as regenerable: sock/, certs/ (re-issue via ACME). Settings: backup/restic.env.
set -euo pipefail
# shellcheck source=lib.sh
. "$(dirname "$0")/lib.sh"

# Single instance: an overlapping run is skipped, not failed (no alert).
exec 9>"$DIR/.lock"
if ! flock -n 9; then
  log "another backup run holds the lock — skipping"
  exit 0
fi

# A user unit can't order itself after network-online.target (a system target, invisible to the
# user manager), so a Persistent=true catch-up run right after boot may start before the network.
# Wait up to 5 min for the SFTP port before failing.
for i in $(seq 1 30); do
  timeout 5 bash -c ": </dev/tcp/$BACKUP_SFTP_HOST/$BACKUP_SFTP_PORT" 2>/dev/null && break
  [ "$i" -eq 30 ] && { log "$BACKUP_SFTP_HOST:$BACKUP_SFTP_PORT unreachable after 5 min"; exit 1; }
  sleep 10
done

log "$(restic version | head -1); repo $RESTIC_REPOSITORY"
# Init only when restic says the repo doesn't exist (exit 10, restic >= 0.17). Any other
# failure (network, auth, wrong password) must fail the run with its real error, not try init.
rc=0
"${R[@]}" cat config >/dev/null || rc=$?
if [ "$rc" -eq 10 ]; then
  log "repo does not exist — restic init"
  "${R[@]}" init
elif [ "$rc" -ne 0 ]; then
  log "cannot open repo (restic exit $rc)"
  exit "$rc"
fi

# --stdin-from-command: restic fails the snapshot if the command exits non-zero.
# DB BEFORE roles: a tenant provisioned in between then has its t_<id> role in the (later) roles
# snapshot even though the DB dump doesn't know it yet — an extra role is harmless on restore.
# The other order could leave the DB dump granting to a role the roles dump lacks, which fails
# pg_restore on that tenant's grants.
log "backup db-booqr"
"${R[@]}" backup --host "$HOST_TAG" --tag db-booqr --stdin-filename booqr.dump \
  --stdin-from-command -- \
  docker exec "$PG" sh -c "$PG_ENV pg_dump -Fc -d \"\$POSTGRES_DB\""

log "backup db-roles"
"${R[@]}" backup --host "$HOST_TAG" --tag db-roles --stdin-filename roles.sql \
  --stdin-from-command -- \
  docker exec "$PG" sh -c "$PG_ENV pg_dumpall --roles-only"

log "backup config"
excludes=()
for x in "${CONFIG_EXCLUDES[@]}"; do excludes+=(--exclude "$BASE/$x"); done
config_paths=("$BASE")
for d in "$HOME/.config/systemd/user" "$HOME/.config/docker"; do
  [ -d "$d" ] && config_paths+=("$d")
done
"${R[@]}" backup --host "$HOST_TAG" --tag config "${excludes[@]}" "${config_paths[@]}"

log "retention 7d/4w/6m + prune"
"${R[@]}" forget --host "$HOST_TAG" --group-by host,tags \
  --keep-daily 7 --keep-weekly 4 --keep-monthly 6 --prune

# Repo is small (KB–MB): verify every data blob weekly, not just metadata.
if [ "$(date +%u)" -eq 7 ]; then
  log "Sunday — restic check --read-data"
  "${R[@]}" check --read-data
fi
log "done"
