# Off-site backup (optional)

Nightly [restic](https://restic.net/) backup over SFTP, e.g. to a Hetzner Storage Box sub-account, run as the service user from a systemd user timer.

## What is backed up (3 snapshots per run)

| Tag        | Content |
|------------|---------|
| `db-booqr` | `pg_dump -Fc` of the booqr DB, streamed via `--stdin-from-command` (no plaintext dump on disk) |
| `db-roles` | `pg_dumpall --roles-only`: `booqr_*` and per-tenant `t_<id>` roles live at cluster level, not in `pg_dump` |
| `config`   | the project dir (compose, `.env`, `env/*.env`, `initdb/`, `web-gateway/`, scripts) + `~/.config/systemd/user` + `~/.config/docker` |

`env/*.env` holds the **tenant master secret**: every tenant role password is derived from it, so a DB restore is useless without the `config` snapshot. Excluded as regenerable: `sock/`, `certs/` (re-issue via ACME), and `backup/restic.env` (the repo password; keep it in a password manager instead).

The DB is dumped **before** the roles: a tenant provisioned in between then only leaves an extra role in the roles dump, which is harmless on restore. Retention is 7 daily / 4 weekly / 6 monthly per tag, with `restic check --read-data` on Sundays. `restic init` runs only on restic exit 10 (repo missing, restic ≥ 0.17); any other error fails the run. Failure → `OnFailure=` → ntfy (`NTFY_URL`), retried for ~1 min. Before connecting, the backup waits up to 5 min for the SFTP host: a user unit can't wait for `network-online.target`, so a catch-up run right after boot may start before the network is up.

## Setup (as the service user, in the project dir)

1. `sudo apt install restic` (as an admin).
2. `cp backup/restic.env.example backup/restic.env && chmod 600 backup/restic.env`, then fill it in. Generate `RESTIC_PASSWORD` with `openssl rand -hex 32` and store it in a password manager.
3. Create a dedicated SSH key (`ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519_backup -N ''`), install the public key on the SFTP target, and pin the host key: `ssh-keyscan -p <port> <host> > ~/.ssh/known_hosts.backup`. Compare its fingerprint with the provider's published one.
4. Install the units (they assume the project dir is `~/booqr`; edit `ExecStart=` otherwise):
   ```sh
   cp backup/*.service backup/*.timer ~/.config/systemd/user/
   systemctl --user daemon-reload
   systemctl --user enable --now booqr-backup.timer
   systemctl --user start booqr-backup.service   # first run: creates the repo
   ./backup/restore-test.sh
   ```

A system user (UID < 1000) has no per-user journal; its unit logs land in the system journal: `sudo journalctl _UID=<uid> _SYSTEMD_USER_UNIT=booqr-backup.service`.

## Restore test

`restore-test.sh` restores the latest `db-roles` + `db-booqr` into a throwaway postgres container (no network, tmpfs), compares exact per-table row counts and the role list (incl. `t_<id>`) with the live DB, and compares the `config` snapshot with the live project dir. Run it right after a backup; rows written since then show up as differences.

## Disaster restore (new host)

1. Prepare the host with `host-setup.sh`, copy `backup/` + a filled-in `backup/restic.env` + the SSH key, and install `restic`.
2. Restore config as the service user: `restic restore latest --tag config --target /` → project dir, `env/*.env`, units.
3. Start postgres with an **empty data dir and without the role-creating initdb**: move `initdb/` aside, set `POSTGRES_DB=postgres` for this first start, `docker compose up -d postgres`.
4. Roles: `restic dump --tag db-roles latest /roles.sql | sed -E '/^CREATE ROLE "?<POSTGRES_USER>"?;$/d' | docker compose exec -T postgres psql -U <POSTGRES_USER> -d postgres`. The image already created the bootstrap superuser, and it must keep the original name: PG16+ dumps memberships `GRANTED BY <superuser>`, and only the bootstrap superuser has implicit ADMIN on every role.
5. DB: `restic dump --tag db-booqr latest /booqr.dump | docker compose exec -T postgres pg_restore -U <POSTGRES_USER> -d postgres --create --exit-on-error`.
6. Put `initdb/` back, restore `POSTGRES_DB`, `docker compose up -d`, re-issue the TLS certificate.
