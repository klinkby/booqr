#!/bin/sh
# WORKAROUND — rootlesskit 2.0.2 (Debian 13) starts pasta with --no-ra and configures no IPv6
# inside its network namespace. pasta still accepts inbound IPv6 TCP on the host, but the
# connection then hangs (mid-TLS), and browsers can't fall back to IPv4 because the connect
# succeeded. Local tests from the host pass anyway (pasta splices them over loopback), so
# always test IPv6 from another machine.
#
# Run as ExecStartPost= of the rootless user docker.service (host-setup.sh --ipv6-netns-fix
# wires this up): enters rootlesskit's namespaces and gives tap0 the host's global IPv6
# address as a /128 plus a default route via the host's IPv6 gateway. Idempotent.
# Drop it once rootlesskit configures IPv6 in the namespace itself.
set -eu

state=${XDG_RUNTIME_DIR:?}/dockerd-rootless
pid=
for _ in $(seq 1 30); do
  [ -s "$state/child_pid" ] && pid=$(cat "$state/child_pid") && [ -d "/proc/$pid" ] && break
  pid=
  sleep 1
done
[ -n "$pid" ] || { echo "ipv6-netns: rootlesskit child_pid not found in $state" >&2; exit 1; }

# Host side: default IPv6 route → interface + gateway, then that interface's global address
route=$(ip -6 route show default | awk '{ for (i = 1; i < NF; i++) { if ($i == "via") gw = $(i+1); if ($i == "dev") dev = $(i+1) } } END { print gw " " dev }')
gw=${route% *}
dev=${route#* }
if [ -z "$gw" ] || [ -z "$dev" ]; then
  echo "ipv6-netns: host has no IPv6 default route; nothing to do" >&2
  exit 0
fi
addr=$(ip -6 -o addr show dev "$dev" scope global -temporary -deprecated | awk '{ split($4, a, "/"); print a[1]; exit }')
[ -n "$addr" ] || { echo "ipv6-netns: no global IPv6 address on $dev" >&2; exit 1; }

ns() { nsenter -U --preserve-credentials -n -t "$pid" "$@"; }
for _ in $(seq 1 30); do ns ip link show tap0 >/dev/null 2>&1 && break; sleep 1; done
ns ip -6 addr replace "$addr/128" dev tap0 nodad
ns ip -6 route replace default via "$gw" dev tap0
echo "ipv6-netns: tap0 $addr/128, default via $gw"
