#!/bin/bash
# Restore the latest db-roles + db-booqr snapshots into a throwaway postgres (no network,
# tmpfs data) and compare exact per-table row counts and the role list with the live DB, then
# restore the config snapshot and compare it with the live project dir.
# Live rows written after the snapshot can make a count differ; rerun right after a backup.
# Run as the service user.
set -euo pipefail
# shellcheck source=lib.sh
. "$(dirname "$0")/lib.sh"
# shellcheck disable=SC1091
. "$BASE/.env"        # interpolation-only: POSTGRES_IMAGE, POSTGRES_DB, POSTGRES_USER (no secrets)

T=booqr-restore-test
W=$(mktemp -d)
cleanup() { docker rm -f "$T" >/dev/null 2>&1 || true; }
# $W/cfg holds a restored copy of env/*.env (every secret): always remove it
trap 'cleanup; rm -rf "$W"' EXIT
cleanup

"${R[@]}" snapshots --host "$HOST_TAG" --latest 1 --compact

log "starting scratch postgres ($POSTGRES_IMAGE)"
# Same bootstrap superuser name as live: PG16+ dumps memberships "GRANTED BY <grantor>", and
# only the bootstrap superuser has implicit ADMIN on every role. A different superuser name
# makes the role restore fail ("grantor must have the ADMIN option").
# POSTGRES_DB=postgres: otherwise the image pre-creates a DB named after the user, and
# pg_restore --create (which also restores DB-level settings/grants) collides with it.
docker run -d --name "$T" --network none --tmpfs /var/lib/postgresql \
  -e POSTGRES_USER="$POSTGRES_USER" -e POSTGRES_DB=postgres \
  -e POSTGRES_HOST_AUTH_METHOD=trust "$POSTGRES_IMAGE" >/dev/null
for _ in $(seq 60); do
  docker exec "$T" pg_isready -q -h 127.0.0.1 -U "$POSTGRES_USER" && break
  sleep 1
done
docker exec "$T" pg_isready -q -h 127.0.0.1 -U "$POSTGRES_USER"

log "restore roles"
# The image already created the bootstrap superuser: drop only its CREATE ROLE (its ALTER ROLE,
# incl. the password hash, still applies).
"${R[@]}" dump --host "$HOST_TAG" --tag db-roles latest /roles.sql \
  | sed -E "/^CREATE ROLE \"?${POSTGRES_USER}\"?;\$/d" \
  | docker exec -i "$T" psql -X -q -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d postgres
log "restore booqr DB"
"${R[@]}" dump --host "$HOST_TAG" --tag db-booqr latest /booqr.dump \
  | docker exec -i "$T" pg_restore -U "$POSTGRES_USER" -d postgres --create --exit-on-error

# Exact row counts for every base table in public + app (as superuser: bypasses RLS).
# The SQL goes over stdin; \gexec runs each generated count query.
COUNT_SQL="select format('select %L, count(*) from %I.%I', table_schema||'.'||table_name,
  table_schema, table_name) from information_schema.tables
  where table_schema in ('public','app') and table_type='BASE TABLE' order by 1 \gexec"
# Plus one row per non-builtin role (incl. per-tenant t_<id>): a missing role means that
# tenant can't log in after a restore even though its rows are all there.
ROLE_SQL="select 'role:'||rolname||' '||(case when rolcanlogin then 'login' else 'nologin' end)
  from pg_roles where rolname !~ '^pg_' order by 1"
scratch() { docker exec -i "$T" psql -X -At -F ' ' -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"; }
live() { docker exec -i "$PG" sh -c "$PG_ENV psql -X -At -F ' ' -v ON_ERROR_STOP=1 -d \"\$POSTGRES_DB\""; }
{ scratch <<<"$COUNT_SQL"; scratch <<<"$ROLE_SQL"; } | LC_ALL=C sort > "$W/restored"
{ live <<<"$COUNT_SQL"; live <<<"$ROLE_SQL"; } | LC_ALL=C sort > "$W/live"

echo
printf '%-45s %10s %10s\n' TABLE RESTORED LIVE
LC_ALL=C join -a1 -a2 -e - -o 0,1.2,2.2 "$W/restored" "$W/live" \
  | while read -r t a b; do printf '%-45s %10s %10s %s\n' "$t" "$a" "$b" "$([ "$a" = "$b" ] || echo '<-- differs')"; done
tables=$(grep -vc '^role:' "$W/restored" || true)
roles=$(grep -c '^role:' "$W/restored" || true)
diffs=$(LC_ALL=C join -a1 -a2 -e - -o 1.2,2.2 "$W/restored" "$W/live" | awk '$1!=$2' | wc -l)

# Config snapshot: restore to scratch dir and compare with the live project dir
# (env/*.env carries the tenant master secret — without it a restored DB is useless).
log "restore config snapshot"
"${R[@]}" restore --host "$HOST_TAG" --tag config latest --target "$W/cfg" >/dev/null
diff_x=()
for x in "${CONFIG_EXCLUDES[@]}"; do diff_x+=(-x "$(basename "$x")"); done
cfg_diffs=$(diff -rq "${diff_x[@]}" "$W/cfg$BASE" "$BASE" | wc -l || true)
cfg_files=$(find "$W/cfg$BASE/env" -maxdepth 1 -name '*.env' -type f | wc -l)

echo
if [ "$tables" -gt 0 ] && [ "$roles" -gt 0 ] && [ "$diffs" -eq 0 ] \
   && [ "$cfg_diffs" -eq 0 ] && [ "$cfg_files" -ge 5 ]; then
  log "RESTORE TEST PASSED — $tables tables + $roles roles match live; config ($cfg_files env files) identical"
else
  [ "$cfg_diffs" -eq 0 ] || diff -rq "${diff_x[@]}" "$W/cfg$BASE" "$BASE" || true
  log "RESTORE TEST FAILED — tables=$tables roles=$roles row/role diffs=$diffs config diffs=$cfg_diffs env files=$cfg_files"
  exit 1
fi
