# redist/ — deployment notes for agents

Production layout for **rootless Docker** under an unprivileged service user. Prerequisites and the why: `../README.md` § Deployment Prerequisites.

- **Rootless only.** `proxy` uses `network_mode: host` (= rootlesskit netns, pasta `implicit` port driver) and binds 80/443 itself; don't add `ports:`, docker networks for the proxy, or `user:` (HAProxy drops to `haproxy` via `global user/group`). Keep client source IPs intact.
- **Unix sockets, no TCP.** app/api listen on `sock/{app,api}` (apps create them 0666); postgres is `network_mode: none`, clients mount its socket `:ro`. `egress` network is api + worker only.
- **Compartmented secrets.** `.env` = compose interpolation only (no secrets). Each `env/*.env` goes to only the services listed in the compose header; never widen. Tenant master secret must match in `api.env` and `admin.env`.
- **Images pinned** `tag@sha256:` in `.env.example`; bump both together.
- **Postgres:** keep `POSTGRES_INITDB_ARGS=--auth-local=scram-sha-256 …` (default `local trust` = superuser via the socket) and the `pg_isready -h 127.0.0.1` healthcheck (socket probe passes mid-initdb).
- **No host specifics** (IPs, volume/disk names, subuid ranges, real secrets) in any file here — use placeholders.
- **Scripts:** `host-setup.sh` (root, idempotent, `--dry-run`, asks before masking rootful docker), `project-setup.sh` (service user, generates secrets, never overwrites), `set-role-passwords.sh` (after first `up`), `rootless-ipv6-netns.sh` (rootlesskit 2.0.2 IPv6 workaround; test v6 from outside). POSIX `sh` except `host-setup.sh` (bash); keep them `shellcheck`-clean and idempotent.
- **backup/** (optional): restic over SFTP; settings only in `backup/restic.env` (gitignored), shared setup in `backup/lib.sh` (bash). Keep the DB dump BEFORE the roles dump, the `config` snapshot covering `env/*.env`, and `restore-test.sh` passing.
- **Keep in sync:** `docker-compose.yml` ↔ `web-gateway/haproxy.cfg` (binds, socket paths, cert path) ↔ `env/*.env.example` ↔ `project-setup.sh` (generated keys) ↔ README § Deployment Prerequisites ↔ `backup/` (config snapshot paths/excludes).
