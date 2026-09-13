# Operator Guide — Multi-Tenant Booqr

> Operational runbook for the shared-schema + RLS + per-tenant-role model. Architectural rationale lives in
> the repo's `AGENTS.md` files; the `admin` CLI commands referenced here are implemented in
> `src/Klinkby.Booqr.Control/`. This guide assumes that design is deployed.

## 1. Architecture at a glance (operator view)

- **One database, one shared `app` schema.** Every business's data lives in the same tables, separated by a
  `tenant_id` column and enforced by **Row-Level Security** — the database itself refuses cross-tenant reads.
- **One PostgreSQL login role per tenant** (`t_<id>`). The API connects as the tenant's role; RLS keys off
  the connected role (`current_user`), so isolation does not depend on any request-time setting.
- **Control plane vs data:** the Postgres container's one-time `initdb` bootstraps only the control plane
  (schemas, roles, the `public.tenants` registry, `schema_migrations`, the `app.tenant_of()` function,
  default privileges). The **business tables are created and evolved by migrations** run through the `admin`
  container — a single authoritative schema definition.
- **Admin container** holds the elevated roles and is **not** internet-facing. The public API never has
  DDL/role privileges.

## 2. Roles, secrets, and environment

| Role | Privilege | Lives in |
|---|---|---|
| `booqr_registry` | `SELECT` on `public.tenants` | API (host→tenant resolution) |
| `t_<id>` (per tenant) | DML on `app` via `booqr_tenant` group; `NOBYPASSRLS`; owns nothing | API (data access) |
| `booqr_migrator` | `CREATE`/`CREATEROLE`, owns `app`/`public` objects | admin container only |
| `booqr_batch` | `BYPASSRLS` for cross-tenant scheduled jobs | admin/worker context only |

**Secrets / env:**
- `TENANCY__MASTER_SECRET` — HMAC key used to derive each tenant role's password
  (`base64url(HMAC-SHA384(master_secret, tenant_id))`). Required by **both** the API (to connect as tenants)
  and the admin container (to set/rotate passwords). Store in your secret manager; never in the image or VCS.
- API connection string uses the **registry** role only. Admin connection string uses **migrator** (+ batch)
  credentials.
- **Do not** give the API the `POSTGRES_USER`/superuser credentials (the pre-multi-tenant compose did — it
  must be changed).

> **Security invariant:** compromising the API process can derive tenant passwords (a known, accepted
> boundary). It cannot create/drop schemas or roles, and RLS still contains query-level cross-tenant access.
> If you need to contain full app compromise, move tenant credentials to a per-tenant secret store.

## 3. First-time bring-up

1. Provision the database with the control-plane `initdb` scripts mounted (fresh/empty data dir triggers
   them). Confirm `public.tenants`, `public.schema_migrations`, the four roles, `app` schema, and
   `app.tenant_of()` exist.
2. Set `TENANCY__MASTER_SECRET` and the registry/migrator connection strings in the environment.
3. Build the app schema to the latest version:
   ```
   docker compose run --rm admin --migrate
   ```
4. Provision the first tenant (creates the role, grants membership, seeds the initial admin user, prints a
   tenant-bound activation link):
   ```
   docker compose run --rm admin --provision <slug>
   ```
5. Point DNS + TLS at the proxy (see §6), then hand the activation link to the tenant's administrator.

## 4. Tenant lifecycle

- **Provision:** `docker compose run --rm admin --provision <slug>`
  - `<slug>` must be a DNS label (`^[a-z0-9]([a-z0-9-]{0,30}[a-z0-9])?$`). Reserved subdomains are the
    `Tenancy:ReservedSubdomains` config list (currently `www`, `status`, `mta-sts`) plus the apex.
  - Refuses to run if role `t_<id>` already exists (never adopts an orphan).
  - Output includes the tenant id, and the initial-admin activation link — deliver it over a trusted channel.
- **Deprovision:** `docker compose run --rm admin --deprovision <id>`
  - Marks `tenants.deleted`, drops/locks `t_<id>`. The subdomain then resolves to `tenant-not-found` (browser
    is redirected to `www` by the SPA) within the registry cache TTL.
  - The slug becomes reusable; a new tenant with the same slug gets a **new id and role** — old data is never
    re-attached.
- **List/inspect tenants:** query `public.tenants` as `booqr_registry` (or the migrator). Deleted tenants
  have a non-null `deleted`.
- **Data export for one tenant** (e.g. offboarding): connect as `booqr_batch` (BYPASSRLS) and filter
  `WHERE tenant_id = <id>` per table. Never hand a tenant another tenant's export.

## 5. Migrations

- Applied only through the admin container: `docker compose run --rm admin --migrate`.
- Because the schema is shared, each migration is **atomic across all tenants** — one `ALTER TABLE` covers
  everyone. Each script runs in a transaction under a Postgres **advisory lock** (safe with concurrent
  deploys); a failure rolls back and exits non-zero (**fail the deploy**, don't ignore).
- **RLS coverage guard:** migration/CI fails if any `app` table lacks `ENABLE`+`FORCE ROW LEVEL SECURITY`
  with the tenant policy. Never disable this guard to get a migration through.
- **Rollout order for breaking changes:** expand (add columns/tables, migrate) → deploy new API → contract
  (drop old) in a later migration. The API must tolerate the intermediate state.
- New tables are auto-granted to `booqr_tenant`/`booqr_batch` via default privileges — no per-tenant grant
  step. Migration authors must include the `tenant_id` default + `FORCE RLS` + policy on every new table.

## 6. DNS, TLS, and the proxy

- **DNS:** wildcard `*.booqr.dk` → the proxy. **TLS:** a wildcard certificate covering `*.booqr.dk`.
- **HAProxy** accepts `*.booqr.dk`, routes `/api/` → API and everything else → the SPA, and redirects **only
  the naked apex** `booqr.dk` → `www.booqr.dk` (an exact match — do not restore the old prefix rule, which
  wrongly rewrote `alice.booqr.dk` → `www.alice.booqr.dk`).
- The API's host authority is the preserved `Host`; client `X-Forwarded-Host` is ignored. `AllowedHosts` is
  the `*.booqr.dk` wildcard.
- Unknown/deleted subdomains are **not** an edge concern — the SPA is served for any `*.booqr.dk`, and
  validity is decided by `GET /api/my-tenant` (404 → browser redirect to `www`).

## 7. Credential rotation

- Rotate the master secret with `docker compose run --rm admin --rotate` (re-`ALTER ROLE … PASSWORD` for
  every tenant under a new `TENANCY__MASTER_SECRET`). Sequence: stage the new secret where the admin can read
  it, run `--rotate`, then roll the API to the new secret. Plan a brief window where in-flight pooled
  connections may need to re-establish.
- Rotate registry/migrator credentials via your normal Postgres role-password process; update the
  corresponding connection strings.

## 8. Capacity & connection budget

- Npgsql pools are per-role, so **one pool per active tenant**. Budget:
  `peak backends ≈ replicas × active-tenants × per-tenant Max Pool Size`.
- Keep per-tenant `Max Pool Size` small (2–3) and `Min Pool Size = 0`; the API caches data sources in a
  bounded LRU and evicts idle tenants. **Watch `max_connections`** — if the budget approaches it, either
  raise `max_connections`/add RAM, reduce the LRU ceiling, or shard tenants across DB instances. (PgBouncer
  transaction-mode pooling is **not** supported by this design.)
- Metrics to alert on: Postgres `numbackends` vs `max_connections`; connection-acquire wait/timeout on the
  API; active-data-source count in the LRU.

## 9. Backups & restore

- Standard whole-database `pg_dump`/PITR covers all tenants at once (they share the schema).
- Single-tenant restore is **not** a simple table restore (shared tables). Use a filtered logical export
  (`WHERE tenant_id = <id>`, as `booqr_batch`) for tenant-level offboarding/migration, and restore into a
  provisioned tenant. Test this path before you need it.

## 10. Monitoring & troubleshooting

- **`tenant-not-found` for a real tenant:** check `public.tenants` row (`deleted` null? correct `slug`?),
  the registry cache TTL, and that DNS/host reaches the API with the right `Host`.
- **Connection exhaustion / acquire timeouts:** see §8 — too many active tenants × pool size; check the LRU
  ceiling and `max_connections`.
- **Suspected isolation problem:** run the RLS coverage query (any `app` table with
  `relrowsecurity=false OR relforcerowsecurity=false` in `pg_class`) — should return zero rows. Verify the
  API is connecting as `t_<id>` (not migrator/batch/superuser) — a table-owner or `BYPASSRLS` connection
  bypasses RLS. **Treat any confirmed cross-tenant read as a security incident**, not a bug ticket.
- **Migration failed mid-deploy:** it rolled back (advisory lock + transaction). Fix the script and re-run
  `--migrate`; do not hand-edit the schema. Confirm `public.schema_migrations` reflects the true version.
- **Login/reset link "not working" across a subdomain:** expected — links are tenant-scoped; a link for one
  tenant used on another's host is rejected. Re-issue on the correct subdomain.

## 11. Security operations checklist (periodic)

- API role has **no** `CREATE`/`CREATEROLE`/`BYPASSRLS`; tenant roles are `NOBYPASSRLS` and own nothing.
- `booqr_migrator`/`booqr_batch` credentials exist **only** in the admin container, which is off the public
  backend.
- RLS coverage query returns zero unprotected `app` tables.
- `TENANCY__MASTER_SECRET` and DB credentials are in the secret manager, not images/VCS/logs; no tenant ids,
  tokens, or PII in logs.
- Wildcard TLS cert current; HAProxy apex redirect is the exact-match form.
