#!/bin/bash
# Prepare a Debian 13 host to run the booqr stack under rootless Docker.
#
#   sudo ./host-setup.sh [--dry-run] [--ipv6-netns-fix] [--data-dir DIR] [USER]
#
# USER defaults to svc-booqr. Idempotent: every step checks before it changes anything, so it
# is safe to re-run. Does:
#   - install docker.io, docker-compose, uidmap, rootlesskit, passt, openssl
#   - create USER (system, nologin, no sudo, not in the docker group), subuid/subgid, linger
#   - lower net.ipv4.ip_unprivileged_port_start to 80 (proxy binds 80/443 in the rootless netns)
#   - write USER's rootless docker.service user unit (pasta + implicit port driver + IPv6)
#   - optionally (--ipv6-netns-fix) add the rootlesskit IPv6 namespace workaround
#   - optionally (--data-dir) create the postgres data dir owned by the mapped container UID 70
#   - ASK before disabling + masking rootful docker/containerd
# Does NOT touch the firewall, SSH, TLS certificates or DNS — see README "Deployment Prerequisites".
set -euo pipefail

usage() { sed -n '4p' "$0" | sed 's/^# *//' >&2; exit 2; }

DRY_RUN=0
IPV6_FIX=0
DATA_DIR=
SVC_USER=svc-booqr
while [ $# -gt 0 ]; do
  case "$1" in
    --dry-run) DRY_RUN=1 ;;
    --ipv6-netns-fix) IPV6_FIX=1 ;;
    --data-dir) [ $# -ge 2 ] || usage; DATA_DIR=$2; shift ;;
    -h|--help) usage ;;
    -*) usage ;;
    *) SVC_USER=$1 ;;
  esac
  shift
done

HERE=$(cd "$(dirname "$0")" && pwd)
CONTRIB=/usr/share/docker.io/contrib
SUBID_COUNT=65536

log() { printf '==> %s\n' "$*"; }
warn() { printf 'WARNING: %s\n' "$*" >&2; }
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }
run() {
  if [ "$DRY_RUN" = 1 ]; then printf '    [dry-run] %s\n' "$*"; else "$@"; fi
}
ask() { # ask "question" → 0 on y/yes
  local reply
  if [ "$DRY_RUN" = 1 ]; then printf '    [dry-run] would ask: %s\n' "$1"; return 1; fi
  read -r -p "$1 [y/N] " reply 2>/dev/null </dev/tty || return 1
  case "$reply" in y|Y|yes|YES) return 0 ;; *) return 1 ;; esac
}
# write_file PATH MODE OWNER < content — only if the content differs; sets WROTE=1 when it writes
WROTE=0
write_file() {
  local path=$1 mode=$2 owner=$3 tmp
  tmp=$(mktemp)
  cat > "$tmp"
  if [ -f "$path" ] && cmp -s "$tmp" "$path"; then
    rm -f "$tmp"; return 0
  fi
  log "write $path"
  WROTE=1
  if [ "$DRY_RUN" = 1 ]; then
    sed 's/^/    | /' "$tmp"; rm -f "$tmp"; return 0
  fi
  if [ "$owner" = root:root ]; then
    install -D -m "$mode" -o root -g root "$tmp" "$path"
  else
    # Write as the owner, never as root: the owner controls that tree and could plant a symlink
    # (e.g. → /etc/shadow) that a root write or chown would follow. Content goes over stdin
    # because the root-owned mktemp file isn't readable by the owner.
    runuser -u "${owner%%:*}" -- install -D -m "$mode" /dev/stdin "$path" < "$tmp"
  fi
  rm -f "$tmp"
}

[ "$(id -u)" = 0 ] || die "run as root (sudo $0 ...)"

# --- OS check ---------------------------------------------------------------------------------
. /etc/os-release
if [ "${ID:-}" != debian ] || [ "${VERSION_ID:-}" != 13 ]; then
  warn "written for Debian 13; this is ${PRETTY_NAME:-unknown}. Package names and the location of dockerd-rootless.sh may differ."
  ask "Continue anyway?" || exit 1
fi

# --- packages ---------------------------------------------------------------------------------
PKGS=(docker.io docker-compose uidmap rootlesskit passt openssl)
missing=()
for p in "${PKGS[@]}"; do
  dpkg-query -W -f='${Status}' "$p" 2>/dev/null | grep -q 'install ok installed' || missing+=("$p")
done
if [ ${#missing[@]} -gt 0 ]; then
  log "install ${missing[*]}"
  run apt-get update -q
  run env DEBIAN_FRONTEND=noninteractive apt-get install -y -q "${missing[@]}"
fi
[ "$DRY_RUN" = 1 ] || [ -x "$CONTRIB/dockerd-rootless.sh" ] || die "$CONTRIB/dockerd-rootless.sh not found"

# --- service user -----------------------------------------------------------------------------
if ! id "$SVC_USER" >/dev/null 2>&1; then
  log "create user $SVC_USER"
  run useradd --system --create-home --shell /usr/sbin/nologin "$SVC_USER"
fi
if [ "$DRY_RUN" = 1 ] && ! id "$SVC_USER" >/dev/null 2>&1; then
  log "(dry-run: user does not exist yet; remaining per-user steps are shown with placeholders)"
  SVC_UID='<uid>'; SVC_HOME="/home/$SVC_USER"
else
  SVC_UID=$(id -u "$SVC_USER")
  SVC_HOME=$(getent passwd "$SVC_USER" | cut -d: -f6)
  [ "$(stat -c %a "$SVC_HOME")" = 700 ] || run chmod 700 "$SVC_HOME"
  id -nG "$SVC_USER" | tr ' ' '\n' | grep -qx docker && warn "$SVC_USER is in the docker group (root-equivalent); remove it: gpasswd -d $SVC_USER docker"
  id -nG "$SVC_USER" | tr ' ' '\n' | grep -qx sudo && warn "$SVC_USER is in the sudo group; remove it: gpasswd -d $SVC_USER sudo"
fi

# --- subuid / subgid --------------------------------------------------------------------------
# --system users get none automatically. Allocate the next free range after every existing one.
subid_start() { awk -F: -v u="$SVC_USER" '$1 == u { print $2; exit }' "$1" 2>/dev/null; }
if [ -z "$(subid_start /etc/subuid)" ] || [ -z "$(subid_start /etc/subgid)" ]; then
  next=$(cat /etc/subuid /etc/subgid 2>/dev/null | awk -F: 'BEGIN { m = 100000 } NF == 3 && $2 + $3 > m { m = $2 + $3 } END { print m }')
  last=$((next + SUBID_COUNT - 1))
  log "allocate subuid/subgid $next-$last to $SVC_USER"
  [ -n "$(subid_start /etc/subuid)" ] || run usermod --add-subuids "$next-$last" "$SVC_USER"
  [ -n "$(subid_start /etc/subgid)" ] || run usermod --add-subgids "$next-$last" "$SVC_USER"
fi
SUBUID_START=$(subid_start /etc/subuid)
SUBUID_START=${SUBUID_START:-'<subuid start>'}

# --- linger -----------------------------------------------------------------------------------
if [ "$(loginctl show-user "$SVC_USER" -p Linger --value 2>/dev/null)" != yes ]; then
  log "enable linger for $SVC_USER"
  run loginctl enable-linger "$SVC_USER"
fi

# --- privileged ports -------------------------------------------------------------------------
write_file /etc/sysctl.d/99-rootless-ports.conf 644 root:root <<'EOF'
# Let rootless Docker (booqr proxy) bind 80/443 inside its network namespace.
net.ipv4.ip_unprivileged_port_start=80
EOF
if [ "$(cat /proc/sys/net/ipv4/ip_unprivileged_port_start)" != 80 ]; then
  run sysctl -q -p /etc/sysctl.d/99-rootless-ports.conf
fi

# --- IPv6 namespace workaround (optional) -----------------------------------------------------
WROTE=0 # from here on, a write means the docker unit or its hook changed → restart
NETNS_HOOK=
if [ "$IPV6_FIX" = 1 ]; then
  write_file "$SVC_HOME/.config/docker/ipv6-netns.sh" 755 "$SVC_USER:$SVC_USER" < "$HERE/rootless-ipv6-netns.sh"
  # '-': a failing IPv6 hook (e.g. SLAAC not done at boot) must not stop the daemon
  NETNS_HOOK="ExecStartPost=-$SVC_HOME/.config/docker/ipv6-netns.sh"
fi

# --- rootless docker user unit ----------------------------------------------------------------
# Debian's dockerd-rootless-setuptool.sh can't find dockerd-rootless.sh (not on PATH), so the
# unit is written by hand, mirroring what the setup tool generates.
write_file "$SVC_HOME/.config/systemd/user/docker.service" 644 "$SVC_USER:$SVC_USER" <<EOF
[Unit]
Description=Docker Application Container Engine (Rootless)
Documentation=https://docs.docker.com/go/rootless/

[Service]
Environment=PATH=$CONTRIB:/usr/sbin:/usr/bin:/sbin:/bin
# pasta + implicit port driver: host v4+v6 ports go straight into the rootlesskit netns and
# client source IPs survive (the default builtin driver rewrites them to 127.0.0.1).
Environment=DOCKERD_ROOTLESS_ROOTLESSKIT_NET=pasta
Environment=DOCKERD_ROOTLESS_ROOTLESSKIT_PORT_DRIVER=implicit
Environment=DOCKERD_ROOTLESS_ROOTLESSKIT_FLAGS=--ipv6
ExecStart=$CONTRIB/dockerd-rootless.sh
${NETNS_HOOK}
ExecReload=/bin/kill -s HUP \$MAINPID
TimeoutSec=0
RestartSec=2
Restart=always
StartLimitBurst=3
StartLimitInterval=60s
LimitNOFILE=infinity
LimitNPROC=infinity
LimitCORE=infinity
TasksMax=infinity
Delegate=yes
Type=notify
NotifyAccess=all
KillMode=mixed

[Install]
WantedBy=default.target
EOF

# Shell env for `sudo -u $SVC_USER bash` sessions and scripts. Appended as the user, not root
# (a symlinked rc file must not redirect a root write; see write_file).
for rc in .profile .bashrc; do
  if ! grep -qs 'DOCKER_HOST=unix:///run/user/' "$SVC_HOME/$rc"; then
    log "add XDG_RUNTIME_DIR/DOCKER_HOST to $SVC_HOME/$rc"
    if [ "$DRY_RUN" = 0 ]; then
      # shellcheck disable=SC2016 # $1 expands in the user's sh
      printf '\nexport XDG_RUNTIME_DIR=/run/user/%s\nexport DOCKER_HOST=unix:///run/user/%s/docker.sock\n' \
        "$SVC_UID" "$SVC_UID" | runuser -u "$SVC_USER" -- sh -c 'cat >> "$1"' sh "$SVC_HOME/$rc"
    fi
  fi
done

# --- rootful docker ---------------------------------------------------------------------------
# Installing docker.io starts the system daemon. It bypasses the host firewall with its own
# iptables rules and its socket is root-equivalent, so it should not run next to the rootless one.
ROOTFUL_UNITS=(docker.service docker.socket containerd.service)
unmasked=()
for u in "${ROOTFUL_UNITS[@]}"; do
  [ "$(systemctl is-enabled "$u" 2>/dev/null || true)" = masked ] || unmasked+=("$u")
done
if [ ${#unmasked[@]} -gt 0 ]; then
  running=$( (docker ps -q 2>/dev/null || true) | wc -l)
  warn "rootful Docker is not masked (${unmasked[*]}); it currently runs $running container(s)."
  if ask "Disable + mask rootful docker/containerd now? Running rootful containers will be STOPPED."; then
    run systemctl disable --now "${ROOTFUL_UNITS[@]}"
    run systemctl mask "${ROOTFUL_UNITS[@]}"
  else
    warn "leaving rootful Docker as is"
  fi
fi
members=$( (getent group docker || true) | cut -d: -f4)
[ -z "$members" ] || warn "docker group members (root-equivalent while rootful Docker exists): $members — consider: gpasswd -d <user> docker"

# --- start rootless docker --------------------------------------------------------------------
if [ "$DRY_RUN" = 0 ]; then
  for _ in $(seq 1 20); do [ -S "/run/user/$SVC_UID/bus" ] && break; sleep 0.5; done
  [ -S "/run/user/$SVC_UID/bus" ] || die "user manager for $SVC_USER did not start (/run/user/$SVC_UID/bus missing)"
fi
svc_systemctl() { run systemctl --user --machine="$SVC_USER@" "$@"; }
svc_systemctl daemon-reload
svc_systemctl enable docker.service
if [ "$WROTE" = 1 ]; then
  svc_systemctl restart docker.service # unit or hook changed; restarts every container
elif ! systemctl --user --machine="$SVC_USER@" is-active --quiet docker.service 2>/dev/null; then
  svc_systemctl start docker.service
fi

if [ "$DRY_RUN" = 0 ]; then
  ctrl=/sys/fs/cgroup/user.slice/user-$SVC_UID.slice/user@$SVC_UID.service/cgroup.controllers
  for c in cpu memory pids; do
    grep -qw "$c" "$ctrl" 2>/dev/null || warn "cgroup controller '$c' not delegated to $SVC_USER; compose resource limits will fail"
  done
  security=$(runuser -u "$SVC_USER" -- env XDG_RUNTIME_DIR="/run/user/$SVC_UID" \
    DOCKER_HOST="unix:///run/user/$SVC_UID/docker.sock" docker info --format '{{.SecurityOptions}}')
  case "$security" in *rootless*) log "rootless docker is up for $SVC_USER" ;; *) die "docker info does not report rootless" ;; esac
fi

# --- postgres data dir (optional) -------------------------------------------------------------
if [ -n "$DATA_DIR" ]; then
  # container UID/GID 70 (postgres in the alpine image) → host subuid_start + 69
  if [ "$DRY_RUN" = 1 ] && [[ $SUBUID_START == \<* ]]; then
    pg_uid='<subuid start + 69>'
  else
    pg_uid=$((SUBUID_START + 69))
  fi
  if [ ! -d "$DATA_DIR" ]; then
    log "create $DATA_DIR"
    run install -d -m 700 -o "$pg_uid" -g "$pg_uid" "$DATA_DIR"
  elif [ "$(stat -c %u "$DATA_DIR")" != "$pg_uid" ]; then
    if [ -z "$(ls -A "$DATA_DIR")" ]; then
      run chown "$pg_uid:$pg_uid" "$DATA_DIR"; run chmod 700 "$DATA_DIR"
    else
      warn "$DATA_DIR is not empty and not owned by $pg_uid; leaving it alone"
    fi
  fi
fi

cat <<EOF

Host ready. Next:
  1. Copy the redist/ files to the project dir (e.g. $SVC_HOME/booqr), owned by $SVC_USER.
  2. As $SVC_USER, run project-setup.sh there${DATA_DIR:+ --data-dir $DATA_DIR}.
  3. Not done by this script: firewall (allow 80/tcp, 443/tcp, 443/udp), TLS certificate
     (certs/certs.pem), DNS records, mail credentials (env/mail.env).
Operate as $SVC_USER:
  sudo -u $SVC_USER XDG_RUNTIME_DIR=/run/user/$SVC_UID DOCKER_HOST=unix:///run/user/$SVC_UID/docker.sock docker ...
EOF
