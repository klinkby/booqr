#!/bin/sh
# Create the per-deployment files in this directory (a copy of redist/), as the service user:
#
#   ./project-setup.sh [--data-dir DIR]
#
#   .env       from .env.example (POSTGRES_DATA_DIR set if given), mode 600
#   env/*.env  from env/*.env.example with every generated secret filled in, mode 600
#              (JWT + password HMAC keys, role passwords, postgres password, tenant master
#              secrets). Mail credentials are external and stay changeme_ until you edit them.
#   sock/app, sock/api   unix socket dirs (1777: the containers' non-root users create
#                        their sockets here; the project dir itself is not world-readable)
#   certs/               put certs.pem (fullchain + private key in one file) here
#
# Refuses to overwrite existing secrets. Nothing secret is printed.
set -eu
cd "$(dirname "$0")"
umask 077

usage() { sed -n '4p' "$0" | sed 's/^# *//' >&2; exit 2; }
DATA_DIR=
while [ $# -gt 0 ]; do
  case "$1" in
    --data-dir) [ $# -ge 2 ] || usage; DATA_DIR=$2; shift ;;
    *) usage ;;
  esac
  shift
done

[ "$(id -u)" != 0 ] || { echo "run as the service user, not root" >&2; exit 1; }
[ -f .env.example ] && [ -d env ] || { echo "run from a copy of redist/ (no .env.example / env/ here)" >&2; exit 1; }
command -v openssl >/dev/null || { echo "openssl is required" >&2; exit 1; }

# set_var FILE KEY VALUE — replace the KEY= line (values are hex/paths: no sed escaping needed)
set_var() {
  grep -q "^$2=" "$1" || { echo "$1: no $2= line" >&2; exit 1; }
  sed -i "s|^$2=.*|$2=$3|" "$1"
}
rnd() { openssl rand -hex 32; }

# --- .env (interpolation only, no secrets) ---
if [ -e .env ]; then
  echo "keep existing .env"
else
  cp .env.example .env
  [ -z "$DATA_DIR" ] || set_var .env POSTGRES_DATA_DIR "$DATA_DIR"
  chmod 600 .env
  echo "created .env"
fi

# --- env/*.env (secrets) ---
# All or nothing: the tenant master secret must be identical in api.env and admin.env.
existing=$(find env -maxdepth 1 -name '*.env' | wc -l)
if [ "$existing" -gt 0 ]; then
  echo "keep existing env/*.env ($existing file(s)); delete them all to regenerate"
else
  master=$(rnd)
  for f in env/*.env.example; do cp "$f" "${f%.example}"; done

  set_var env/api.env Application__Jwt__Key "$(rnd)"
  set_var env/api.env Application__Password__HmacKey "$(rnd)"
  set_var env/api.env Infrastructure__TenantDataSources__MasterSecret "$master"
  set_var env/api.env Infrastructure__Registry__RegistryPassword "$(rnd)"
  set_var env/worker.env Infrastructure__Batch__BatchPassword "$(rnd)"
  set_var env/admin.env BOOQR_MIGRATOR_PASSWORD "$(rnd)"
  set_var env/admin.env BOOQR_TENANT_MASTER_SECRET "$master"
  set_var env/admin.env BOOQR_TENANT_MASTER_SECRET_NEW "$(rnd)"
  set_var env/postgres.env POSTGRES_PASSWORD "$(rnd)"

  chmod 700 env
  chmod 600 env/*.env
  echo "created env/*.env with generated secrets"
fi

# --- directories ---
mkdir -p sock/app sock/api certs
chmod 1777 sock/app sock/api
chmod 700 certs

# --- what is left ---
todo=$(grep -l 'changeme_' .env env/*.env 2>/dev/null || true)
[ -z "$todo" ] || echo "still has changeme_ placeholders (edit by hand): $todo"
[ -f certs/certs.pem ] || echo "missing certs/certs.pem (fullchain + private key concatenated)"
if [ -n "$DATA_DIR" ] && [ ! -d "$DATA_DIR" ]; then
  echo "missing $DATA_DIR — create it as root: host-setup.sh --data-dir $DATA_DIR"
fi
cat <<'EOF'
Then:
  docker compose up -d
  ./set-role-passwords.sh
  docker compose run --rm admin admin --migrate
  docker compose run --rm admin admin --provision <slug>
EOF
