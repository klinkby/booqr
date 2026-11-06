#!/bin/sh
# Replace the placeholder passwords that initdb/01-control-plane.sql gives the LOGIN roles
# (booqr_registry, booqr_batch, booqr_migrator) with the values in env/*.env.
# Idempotent. Run as the service user from anywhere, with postgres up and healthy.
# Secrets go over stdin to psql; nothing is printed or put on a command line.
set -eu
cd "$(dirname "$0")"

load() {
  set -a
  . ./env/api.env
  . ./env/worker.env
  . ./env/admin.env
  set +a
}

# Validate first: a failure inside the piped subshell below would not stop the script
# (POSIX sh has no pipefail), and psql would happily run on empty input.
(
  load
  # shellcheck disable=SC2154 # assigned by sourcing env/*.env in load()
  for v in "$Infrastructure__Registry__RegistryPassword" "$Infrastructure__Batch__BatchPassword" \
           "$BOOQR_MIGRATOR_PASSWORD"; do
    case "$v" in
      ''|changeme_*) echo "env/*.env still has an empty or changeme_ role password" >&2; exit 1 ;;
    esac
  done
)

(
  load
  # SQL string literal: double any single quote
  q() { printf %s "$1" | sed "s/'/''/g"; }
  printf "alter role booqr_registry password '%s';\n" "$(q "$Infrastructure__Registry__RegistryPassword")"
  printf "alter role booqr_batch password '%s';\n" "$(q "$Infrastructure__Batch__BatchPassword")"
  printf "alter role booqr_migrator password '%s';\n" "$(q "$BOOQR_MIGRATOR_PASSWORD")"
) | docker compose exec -T postgres sh -c \
  'PGPASSWORD="$POSTGRES_PASSWORD" psql -X -q -v ON_ERROR_STOP=1 -h /var/run/postgresql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'

echo "OK — role passwords set"
