# Multi-Tenancy Design — Klinkby.Booqr (Backend)

> Companion: `docs/implementation.md` (orchestration guide for implementation agents).
> Frontend counterpart lives in the `klinkby/booqr-app` repo.

## Context

Booqr is today **single-tenant**: one PostgreSQL database, one flat set of
`users / locations / services / bookings / calendar / employeeservices / refreshtokens / activities`,
no tenant column, no tenant claim, no tenant resolution. Goal: host multiple independent businesses on one
deployment, each seeing only its own data, with isolation **enforced by the database**.

## Locked decisions

- **Isolation model:** **one shared schema** (`app`) whose tables carry a `tenant_id` column, protected by
  **`FORCE ROW LEVEL SECURITY`**, combined with **one PostgreSQL login role per tenant** (`t_<id>`). The RLS
  policy is keyed on the **connected role** (`current_user`) — unforgeable, no per-request session variable.
- **Why:** shared schema → global ids, atomic cross-tenant migrations, simple cross-tenant batch jobs;
  per-tenant role → DB-enforced boundary immune to a forgotten/spoofed GUC. Fits the uneditable query
  generator (`Klinkby.Booqr.Infrastructure.Generators` v1.1.3): a `tenant_id DEFAULT app.tenant_of(current_user)`
  column + RLS policy means the generated `SELECT/INSERT/UPDATE/DELETE` work **unchanged**.
- **Pooling:** **direct / session pooling only** (matches today's unix-socket Npgsql→Postgres). PgBouncer
  transaction-mode is out of scope.
- **Tenant identity = immutable integer `tenants.id`**, never the slug. Role is `t_<id>`; slug is a public
  routing label and is reusable.
- **Users are per-tenant rows** (global `id` sequence, `tenant_id` column, `(tenant_id, email)` unique).
- **Provisioning/migration** run only in a separate **admin container** holding the elevated migrator role.

## The RLS mechanism (the crux)

- Every `app` table: `tenant_id int NOT NULL DEFAULT app.tenant_of(current_user)`.
- `app.tenant_of(login_role name) → int` is `STABLE SECURITY DEFINER`, mapping a login role → tenant id via
  `public.tenants.db_role`. Tenant roles have **no** direct `SELECT` on `public.tenants`; only this function
  reads it.
- Each table: `ENABLE` + `FORCE ROW LEVEL SECURITY`, policy
  `USING (tenant_id = app.tenant_of(current_user)) WITH CHECK (tenant_id = app.tenant_of(current_user))`.
- Result: connected as `t_<id>`, `SELECT * FROM bookings` returns only that tenant's rows; inserts
  auto-stamp `tenant_id`; a row can't be moved to another tenant. **No GUC, nothing to leak across pooled
  connections.**

## Honest isolation boundary

Contains **query-level** cross-tenant access (injection, query bugs, forgotten filters — all fail at the DB).
Residual risks, stated plainly:
- **Shared blast radius:** a wrong/missing policy, or a table shipped without `FORCE RLS`, exposes everyone.
  Mitigations: `FORCE RLS` on every table; policy on unspoofable `current_user`; a migrate-time/test guard
  that fails if any `app` table lacks an enabled+forced policy.
- **App-tier compromise:** the API can derive any tenant role's password (credential model below), so full
  code-execution in the API is not contained — a different threat class, swappable for a per-tenant secret
  store without design change.

---

## 1. Holistic / principal design

One database, **three schema tiers** and **four role classes**.

**Schemas**
- `public` — locked (`REVOKE CREATE ON SCHEMA public FROM PUBLIC`). Holds `public.tenants` (registry) and
  `public.schema_migrations`. Off tenant roles' `search_path`.
- `app` — the single shared set of Booqr tables/views + `app.tenant_of()`. Owned by the migrator; tenant
  roles' `search_path = app`.

**Roles**
1. **`booqr_registry`** — `SELECT` on `public.tenants` only. The API's registry data source resolves
   host→tenant with it; tenant-independent connection path.
2. **`booqr_tenant`** — `NOLOGIN` group role holding the tenant grant set: `USAGE` on `app`,
   `SELECT/INSERT/UPDATE/DELETE` + sequence `USAGE` on all `app` tables, `EXECUTE` on `app.tenant_of`, via
   `ALTER DEFAULT PRIVILEGES` so future tables/sequences are covered automatically.
3. **`t_<id>`** (per tenant, `LOGIN`) — member of `booqr_tenant`; `search_path=app`; `NOBYPASSRLS`; does
   **not** own tables. The API connects as this; RLS confines it.
4. **`booqr_migrator`** — elevated: `CREATE`/`CREATEROLE`, owns `app`/`public` objects, writes
   `public.tenants`. Admin container only. Plus **`booqr_batch`** — `BYPASSRLS` for cross-tenant batch work.

**Credential model.** `t_<id>` password = `base64url(HMAC-SHA256(master_secret, id))`. Migrator sets it at
provision; API derives the same to connect. One `master_secret` (env), no N-secret store; rotation =
migrator re-`ALTER ROLE … PASSWORD` under a new master.

**Request flow.** HAProxy routes `*.booqr.dk` → API → tenant-resolution middleware reads `Request.Host`,
resolves slug→**tenant id + db_role** via the registry data source (cached, bounded TTL), populates a scoped
`ITenantContext`. The scoped `DbConnection` is created **once per scope** from the tenant's data source
(keyed by `t_<id>`), so `Transaction.Begin` and every repository call share one connection. No per-request
`SET` — the role *is* the tenant.

**Connection pooling & budget.** Npgsql pools are keyed by connection string incl. user, so each `t_<id>`
gets its own pool; total ≈ `replicas × active-tenants × MaxPoolSize`. No cross-tenant session-state to leak.
Bound fan-out: per-tenant `Max Pool Size` 2–3, `Min Pool Size=0`, and a **bounded LRU of `NpgsqlDataSource`s
with lease-refcounting** (evict only sources with zero in-flight leases). Document the ceiling vs Postgres
`max_connections`.

## 2a. API (runtime) changes

**Core:** `ITenantContext { int TenantId; string DbRole; bool HasTenant; }` in **Core** (Infra + Application
both consume it). New `Tenant` record + `ITenantRepository` (repurpose the shelved `TenantsRepositoryTests.cs`).
`TenantId` on `AuthenticatedRequest`. `tenant_id` added to the `Activity` record.

**Infrastructure:**
- **Registry data source** (`booqr_registry`) for `TenantRepository`; resolution never uses a tenant
  connection.
- **Tenant data-source factory**: LRU cache keyed by tenant id; each source = base host + role `t_<id>` +
  derived password. The scoped keyed `DbConnection` registration changes from a static string to a factory
  reading `ITenantContext`. `ConnectionProvider`/`Transaction` otherwise unchanged (one connection per
  scope). No `search_path`/GUC code — the role default handles it.
- New `TenantRepository`.

**Application:**
- **Signed links:** ids are now global and RLS confines reads to the tenant role, so a reset link's user id
  maps to exactly one tenant and can't be replayed cross-tenant (query on another tenant's connection
  returns no row). Issue links against the tenant's own subdomain authority (`https://<slug>.booqr.dk`) and,
  belt-and-braces, reject when host-resolved tenant ≠ the target user's tenant. ETag stays concurrency-only.
- **OAuth (`OAuth.cs`):** add a `tenant` claim = tenant id; `LoginCommand` authenticates within the
  host-resolved tenant; a request-time guard rejects `tenant` claim ≠ host tenant (403). DB is the backstop.
- `SignUpCommand` (always Customer) unchanged; the initial Employee/Admin is created by the admin CLI.

**Config:** `TenancySettings` + `[OptionsValidator]` (base domain, reserved subdomains, registry connection,
`master_secret` ref). No tenant list in config. The API uses the **registry** connection to resolve and
derives **tenant** connections; it no longer uses the `POSTGRES_USER` superuser creds current compose passes.

**API endpoints:** anonymous `GET /api/tenant` (resolve from `Host` → public branding, or
`404 tenant-not-found`); tenant-resolution middleware in `ConfigureMiddleware` before `UseAuthorization`;
reserved/apex hosts resolve to no tenant.

> **Naming safety.** Role `t_<id>` uses the integer id — never user-derived, no identifier-injection surface.
> Public **slug** validated as a DNS label (`^[a-z0-9]([a-z0-9-]{0,30}[a-z0-9])?$`).

### SQL changes (concrete DDL)

**Split of responsibility.** `redist/initdb.sql` (the Postgres container's one-time
`docker-entrypoint-initdb.d` hook) is reduced to bootstrapping the **control plane / provisioning engine
only** — no business tables. The `app` business tables are owned by the **migration path**
(`Migrations/0001_baseline.sql`, applied by `admin --migrate`), so there is a **single authoritative
definition** of the app schema instead of DDL duplicated between initdb and migrations. Tests build the
schema the same way prod does: run initdb, then migrate.

1. **`initdb` — control plane only** (runs once, as the Postgres superuser). Two ordered files under the
   mounted `initdb/` dir:
   - `01-control-plane.sql`:
     - `CREATE SCHEMA app; REVOKE CREATE ON SCHEMA public FROM PUBLIC;`
     - `CREATE TABLE public.tenants (id int GENERATED ALWAYS AS IDENTITY PRIMARY KEY, slug varchar(32) NOT NULL, db_role name NOT NULL, display_name text NOT NULL, created timestamptz NOT NULL DEFAULT now(), modified timestamptz NOT NULL DEFAULT now(), deleted timestamptz);`
       `CREATE UNIQUE INDEX idx_tenants_slug ON public.tenants(slug) WHERE deleted IS NULL;`
     - `CREATE TABLE public.schema_migrations (version int PRIMARY KEY, applied timestamptz NOT NULL DEFAULT now());`
     - Roles: `booqr_registry LOGIN`, `booqr_tenant NOLOGIN`, `booqr_migrator LOGIN CREATEROLE`,
       `booqr_batch LOGIN BYPASSRLS`.
     - `GRANT USAGE ON SCHEMA public TO booqr_registry; GRANT SELECT ON public.tenants TO booqr_registry;`
     - `ALTER SCHEMA app OWNER TO booqr_migrator;` (migrator owns all future app objects).
   - `02-provisioning-engine.sql`:
     - `CREATE FUNCTION app.tenant_of(login_role name) RETURNS int LANGUAGE sql STABLE SECURITY DEFINER AS $$ SELECT id FROM public.tenants WHERE db_role = login_role AND deleted IS NULL $$;`
       `REVOKE ALL ON FUNCTION app.tenant_of(name) FROM PUBLIC; GRANT EXECUTE ON FUNCTION app.tenant_of(name) TO booqr_tenant, booqr_batch;`
     - `GRANT USAGE ON SCHEMA app TO booqr_tenant, booqr_batch;`
     - **Default privileges set before any table exists**, so migration-created tables auto-grant:
       `ALTER DEFAULT PRIVILEGES FOR ROLE booqr_migrator IN SCHEMA app GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO booqr_tenant, booqr_batch;`
       `ALTER DEFAULT PRIVILEGES FOR ROLE booqr_migrator IN SCHEMA app GRANT USAGE, SELECT ON SEQUENCES TO booqr_tenant, booqr_batch;`
   - **No business tables here.** initdb is stable and rarely changes.
2. **`Migrations/0001_baseline.sql`** (applied by `admin --migrate`, connected as `booqr_migrator` → so the
   migrator owns the tables and the default privileges above auto-grant `booqr_tenant`/`booqr_batch`):
   - Create every Booqr table under `app` (from today's `initdb.sql` DDL) **plus**
     `tenant_id int NOT NULL DEFAULT app.tenant_of(current_user)`.
   - **bigint promotion:** `bookings.id`, `calendar.id` → `bigint` identity; `calendar.bookingid` FK →
     `bigint`. `activities.id` already `bigint`; `users/services/locations` stay `int`.
   - **GiST** `no_overlapping_events` on `calendar` extended to include `tenant_id` (overlaps scoped per
     tenant, needs `btree_gist`).
   - **Uniqueness re-scope:** `users` unique → `(tenant_id, email)` partial `WHERE deleted IS NULL`.
   - Views (`mybookings`, `bookingdetails`) under `app`.
   - For **every** table: `ENABLE` + `FORCE ROW LEVEL SECURITY` + policy `tenant_isolation`
     `USING (tenant_id = app.tenant_of(current_user)) WITH CHECK (tenant_id = app.tenant_of(current_user))`.
   - Record `version` in `public.schema_migrations`.
3. **`Migrations/000N_*.sql` …** post-baseline changes; each wrapped in one transaction under advisory lock;
   any new table must include the `tenant_id` default, `FORCE RLS`, and the policy (default privileges
   auto-grant).
4. **Migrate-time RLS guard (SQL):** assert no `app` table has `relrowsecurity=false OR relforcerowsecurity=false`
   (`pg_class`), failing the migration if any table is unprotected.
5. **Testcontainers fixture** (`tests/.../ServiceProviderFixture.cs`): apply the initdb control-plane SQL,
   run the migrator (baseline + migrations) to build `app`, then provision two test tenants (roles + registry
   rows) for isolation tests — the exact prod path.

## 2b. Admin CLI (admin mode)

Same Native-AOT image, started in admin mode by a flag (`Program.cs` branches alongside `isMockServer`, runs
the command, exits). Holds `booqr_migrator` (+ `booqr_batch`); **not** on the HAProxy `api` backend
(internal network / `docker compose run --rm admin …`). No `sh`, no SDK. New `admin` service in
`redist/docker-compose.yml`.

- `admin --migrate`: apply pending ordered DDL scripts (via `Chorn.EmbeddedResourceAccessGenerator`) to the
  single `app` schema, each in a transaction under a Postgres advisory lock, recording
  `public.schema_migrations`. Shared schema ⇒ **atomic across all tenants**. Runs the RLS coverage guard.
- `admin --provision <slug>`: validate slug (DNS label) → `INSERT public.tenants` (→ id, `db_role='t_<id>'`)
  → `CREATE ROLE t_<id> LOGIN PASSWORD <derived> NOBYPASSRLS` → `GRANT booqr_tenant TO t_<id>` →
  `ALTER ROLE t_<id> SET search_path = app` → seed the initial admin user row (`tenant_id=id`,
  Employee/Admin) → emit a tenant-bound activation link. Fails if `t_<id>` already exists (never adopt an
  orphan).
- `admin --deprovision <id>`: soft-delete rows (or retain per policy) + `DROP ROLE t_<id>` (or `NOLOGIN`);
  mark `tenants.deleted`. Slug reusable → new tenant gets new id/role.
- `admin --rotate`: re-`ALTER ROLE … PASSWORD` all tenants under a new master secret.

**Background / scheduled work:**
- **Cross-tenant batch** (reminder mail, `FlushTokenService` token cleanup) runs as **`booqr_batch`
  (BYPASSRLS)** — one query across all tenants, then dispatch; no tenant enumeration or per-tenant
  connection.
- **`ActivityBackgroundService`**: request-scope writer stamps `Activity.tenant_id`; the consumer inserts
  via `booqr_batch` setting `tenant_id` explicitly from the queued value.

> **Grants recap:** grants live once on the `booqr_tenant` group + default privileges; tenant roles inherit.
> New migration tables need no per-tenant loop. Tenant roles never own tables and are `NOBYPASSRLS`.
> Migrations run as `booqr_migrator`. The API never has DDL rights.

## 3. Frontend contract (implemented in `booqr-app`)

The backend must satisfy the contract the SPA depends on:
- `GET /api/tenant` (anonymous): resolves the tenant from `Host`; returns public branding (display name,
  logo ref) for a known tenant, or **`404` ProblemDetails `type: tenant-not-found`** for unknown/deleted/
  malformed. **Never a redirect** (wrong for XHR).
- Reserved/apex hosts (`www`, `api`, naked `booqr.dk`) resolve to no tenant (marketing site).
- The SPA redirects the browser to `https://www.booqr.dk` on `tenant-not-found`; the API does not.

## 4. HAProxy + domain

- **Fix the apex redirect.** Current `acl has_www hdr_beg(host) -i www` + blanket redirect turns
  `alice.booqr.dk` into `www.alice.booqr.dk`. Replace with an **exact apex match**: redirect only naked
  `booqr.dk` → `www.booqr.dk`. Accept `*.booqr.dk`; route `/api/` → api, else → app, via exact/suffix host
  matches (not `hdr_beg`). Keep `default_backend deny` for non-`booqr.dk` hosts.
- **Host authority:** authority = `Request.Host` (HAProxy preserves it). Client `X-Forwarded-Host` is
  ignored unless HAProxy overwrites it. `AllowedHosts` becomes the `*.booqr.dk` wildcard.
- DNS: wildcard `*.booqr.dk`; TLS cert covers the wildcard.

## 5. E2E verification

Backend: **`dotnet publish` (Native AOT) and run the produced binary** (build alone is insufficient for AOT),
plus `tests/` under Testcontainers. Required failure cases:
- **RLS enforcement:** as `t_<A>`, `SELECT * FROM app.bookings` returns only A's rows; `INSERT`/`UPDATE`
  with another tenant's id fails `WITH CHECK`.
- **FORCE-RLS coverage guard** (test + migrate-time): fails if any `app` table lacks an enabled+forced policy.
- **Injection containment:** crafted input can't cross tenants under `t_<id>`.
- **Global-id link safety:** a reset link for tenant A's user id, replayed on B's host, finds no row (RLS)
  and is rejected.
- **Missing/mismatched tenant claim** → 403; **refresh/reset replay** across tenants → rejected.
- **Background jobs:** `booqr_batch` cross-tenant queries work; `Activity` written with queued `tenant_id`;
  a non-BYPASSRLS tenant role cannot run the batch query cross-tenant.
- **Provisioning/deprovision:** `--provision` refuses an existing role; reused slug → new id/role; deleted
  tenant stops resolving within the registry cache TTL.
- **Migration atomicity:** an `ALTER TABLE` migration applies once, visible to all tenants; a failing script
  rolls back under the advisory lock.
- **Pooling:** per-tenant pool reuse leaks no state; PgBouncer transaction mode documented unsupported;
  connection budget respected.
- **HAProxy:** `alice.booqr.dk` not rewritten to `www.alice.booqr.dk`; naked→`www`; spoofed
  `X-Forwarded-Host` ignored.

## Review findings → resolution map

| # | Finding | Resolved by |
|---|---|---|
| 1 | Reset/signup links transferable | §1 global ids + RLS; §2a host/tenant check |
| 2 | Background/scheduled jobs lack tenant context | §2b `booqr_batch` BYPASSRLS; `Activity.tenant_id`; Core `ITenantContext` |
| 3 | Deletion / slug reuse reconnects old data | immutable-id role; no schema to orphan; provision refuses existing role; cache TTL |
| 4 | One role = no DB-enforced boundary | §1 per-tenant role + FORCE RLS on `current_user` (+ blast-radius note) |
| 5 | PgBouncer transaction-pooling leak | session-pooling-only; no GUC ⇒ no session state to leak |
| 6 | Per-schema migrations not in lockstep | single shared schema ⇒ migrations atomic across tenants |
| 7 | Resolution needs own path + Core contract | registry role/data source; `ITenantContext` in Core |
| 8 | HAProxy redirect breaks tenant routing | §4 exact apex match + host-authority policy |
| 9 | Pool budget + one-connection-per-scope | per-tenant pool LRU+lease budget; scope-level connection selection |
| 10 | Provisioning yields no usable admin | §2b seed initial admin + activation link |

Scope is limited to tenancy; no unrelated features added.
