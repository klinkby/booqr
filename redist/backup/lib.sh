# shellcheck shell=bash disable=SC2034 # variables are used by the sourcing scripts
# Shared setup for booqr-backup.sh and restore-test.sh (bash; sourced, not run).
# Layout: this file lives in <project>/backup/, next to restic.env; <project> is the compose dir.

DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
BASE=$(dirname "$DIR")
# Own cwd: `sudo -u <svc>` keeps the caller's cwd (often unreadable here), which breaks tools that
# return to their start dir (GNU find). docker compose also needs the project dir.
cd "$BASE" || exit 1

# shellcheck disable=SC1091
. "$DIR/restic.env"
: "${BACKUP_SFTP_USER:?}" "${BACKUP_SFTP_HOST:?}" "${BACKUP_SFTP_PATH:?}" "${RESTIC_PASSWORD:?}"
case "$RESTIC_PASSWORD" in changeme_*) echo "set RESTIC_PASSWORD in $DIR/restic.env" >&2; exit 1 ;; esac
BACKUP_SFTP_PORT=${BACKUP_SFTP_PORT:-22}
BACKUP_SSH_KEY=${BACKUP_SSH_KEY:-$HOME/.ssh/id_ed25519_backup}
BACKUP_SSH_KNOWN_HOSTS=${BACKUP_SSH_KNOWN_HOSTS:-$HOME/.ssh/known_hosts.backup}
RESTIC_SFTP_CONNECTIONS=${RESTIC_SFTP_CONNECTIONS:-2}
HOST_TAG=${BACKUP_HOST_TAG:-booqr}

export RESTIC_PASSWORD
export RESTIC_REPOSITORY="sftp:${BACKUP_SFTP_USER}@${BACKUP_SFTP_HOST}:${BACKUP_SFTP_PATH}"
export DOCKER_HOST=${DOCKER_HOST:-unix://${XDG_RUNTIME_DIR:-/run/user/$(id -u)}/docker.sock}

SSH_CMD="ssh -i $BACKUP_SSH_KEY -p $BACKUP_SFTP_PORT -o BatchMode=yes"
SSH_CMD+=" -o StrictHostKeyChecking=yes -o UserKnownHostsFile=$BACKUP_SSH_KNOWN_HOSTS"
SSH_CMD+=" -o ServerAliveInterval=15"
# Hetzner Storage Boxes reset restic's default 5 concurrent SFTP connections; 2 works.
R=(restic -o "sftp.command=$SSH_CMD ${BACKUP_SFTP_USER}@${BACKUP_SFTP_HOST} -s sftp"
   -o "sftp.connections=$RESTIC_SFTP_CONNECTIONS")

# Live postgres container of this compose project
PG=$(docker compose ps -q postgres)
[ -n "$PG" ] || { echo "postgres service is not running in $BASE" >&2; exit 1; }
# Superuser credentials come from the postgres container's own env (env/postgres.env).
# shellcheck disable=SC2016 # expanded inside the container
PG_ENV='PGPASSWORD="$POSTGRES_PASSWORD" PGHOST=/var/run/postgresql PGUSER="$POSTGRES_USER"'

# Config snapshot: the whole project dir minus regenerable / circular parts. certs/ holds the TLS
# private key but is re-issued by ACME; restic.env holds the repo password (useless inside the repo).
# Paths relative to the project dir.
CONFIG_EXCLUDES=(sock certs backup/.lock backup/restic.env)

log() { printf '%s  %s\n' "$(date -Is)" "$*"; }
