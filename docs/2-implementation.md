# Implementation Guide — Backend Multi-Tenancy (orchestration)

Audience: the **backend orchestration agent**. You coordinate; you spawn **subagents** to implement
well-scoped subtasks in parallel. Read `docs/1-design.md` first — it is the source of truth. This guide is
*how* to build it safely.

## Orchestration model

- **Model policy (per user preference):**
  - **Haiku** subagents for routine, well-bounded, low-risk subtasks (DTO/record edits, wiring, mechanical
    refactors, test scaffolding).
  - **Sonnet** subagents for **security-critical or architecturally demanding** subtasks — anything touching
    RLS/DDL, roles/grants, credential derivation, connection/tenant resolution, auth claims, or signed links.
    These are marked **[SONNET]** below; everything else is **[HAIKU]**.
- **Parallelism:** subtasks are grouped into **phases**. Within a phase, subtasks marked *parallel-safe* can
  run concurrently (they touch disjoint files). **Phases are serialized** — do not start phase N+1 until
  phase N's validation gate passes, because later phases depend on contracts from earlier ones.
- **Each subagent gets:** the relevant section of `docs/1-design.md`, the exact files to touch, the existing
  patterns to mirror (cite file paths), its validation command, and its security checklist item(s). Tell it
  explicitly **not** to add features beyond its subtask.
- **You (orchestrator) own integration:** after each subagent returns, run the phase gate, resolve conflicts,
  and only then fan out the next phase. Never let two subagents edit the same file concurrently.

## Ground rules every subagent must follow

- **Layer boundaries (enforced by `Klinkby.Booqr.Tests` ArchUnit):** Core references nothing third-party;
  Application references Core only (no `Npgsql`/`Dapper`/`System.Data`/`System.IO`/`System.Net`);
  Infrastructure references Core only; Api references all. Tenant *connection selection* lives in
  Infrastructure; tenant *authorization/claims* live in Application; the `ITenantContext` **contract** lives
  in **Core**.
- **Dapper.AOT:** SQL must be passed as an interpolated string literal (`$"{Query}"`) or the interceptor
  won't fire. Do not break existing `[QueryFields]` + generated-const usage. New tenant-scoping needs **no**
  SQL changes (RLS + `DEFAULT` handle it) — do not add `WHERE tenant_id=` to queries.
- **Immutability:** `*Request` records and Core types stay immutable (`record`). Add `TenantId` as an
  immutable member.
- **Determinism in tests:** use `t0` from AutoData, `FakeTimeProvider`; never `DateTime.UtcNow`.
- **Infra tests:** wrap in a transaction and roll back; use Testcontainers via `ServiceProviderFixture`.
- **Conventional commits** (`feat:`, `chore:`, `test:` …). **Run tests + linters before committing.**
- **No scope creep:** implement only what `docs/1-design.md` specifies. Flag anything ambiguous back to the
  orchestrator instead of inventing behavior.

## Work breakdown

### Phase 0 — Control-plane initdb + migration engine + baseline  **[SONNET]** (serialize; everything depends on it)
Implement the **SQL changes** section of `docs/1-design.md` exactly. Note the split: **initdb bootstraps
only the control plane; the migration path owns the business tables.** Because tests build the schema by
running initdb-then-migrate, the migrator is foundational and lands here (not late).
- **initdb** (`initdb/01-control-plane.sql`, `initdb/02-provisioning-engine.sql`): `app`+`public` schemas,
  `REVOKE CREATE ON public`, `public.tenants`, `public.schema_migrations`, the four roles, `app.tenant_of()`
  (`STABLE SECURITY DEFINER`, `EXECUTE` to `booqr_tenant`/`booqr_batch` only, no direct tenant `SELECT` on
  `public.tenants`), and **`ALTER DEFAULT PRIVILEGES … TO booqr_tenant, booqr_batch` before any table
  exists**. No business tables. Update `redist/docker-compose.yml` initdb mount accordingly.
- **`SchemaMigrator`** (Infrastructure): advisory-lock + per-script transaction, records
  `public.schema_migrations`, runs as `booqr_migrator`. Includes the **RLS coverage guard** (fail if any
  `app` table lacks enabled+forced RLS).
- **`Migrations/0001_baseline.sql`**: all Booqr tables under `app` with `tenant_id DEFAULT
  app.tenant_of(current_user)`, `ENABLE`+**`FORCE ROW LEVEL SECURITY`** + `tenant_isolation` policy; bigint
  promotion (`bookings.id`/`calendar.id` + `calendar.bookingid` FK); GiST `no_overlapping_events` extended
  with `tenant_id`; `users` unique → `(tenant_id, email)`; views recreated under `app`.
- **`ServiceProviderFixture`**: apply initdb SQL → run migrator → provision two test tenants.
- **Gate:** fixture builds the schema via the prod path; a smoke integration test shows RLS isolates two
  tenant roles; the coverage guard passes. **Security review required (Sonnet) before merge.**

### Phase 1 — Core contracts  **[HAIKU]** (serialize after Phase 0; small)
- `ITenantContext { int TenantId; string DbRole; bool HasTenant; }` in Core.
- `Tenant` record + `ITenantRepository`; un-shelve/repurpose `TenantsRepositoryTests.cs` as
  `Tenant(schemaName→db_role/id)` shape.
- Add immutable `TenantId` to `AuthenticatedRequest`; add `tenant_id` to the `Activity` record.
- **Gate:** solution compiles; ArchUnit tests still green.

### Phase 2 — Infrastructure  (parallel-safe subtasks after Phase 1)
- **2a [SONNET]** Tenant data-source factory: LRU cache keyed by tenant id; each `NpgsqlDataSource` built
  from base host + role `t_<id>` + **derived password** (`HMAC-SHA256(master_secret, id)`, base64url).
  Lease-refcounted eviction. Register the scoped keyed `DbConnection` via a **factory reading
  `ITenantContext`** (do not select a data source per call — one connection per scope). Keep
  `ConnectionProvider`/`Transaction` behavior identical otherwise.
- **2b [SONNET]** Registry data source (`booqr_registry`) + `TenantRepository` (host/slug → id + db_role),
  with a bounded-TTL cache. Resolution must **never** use a tenant connection.
- **2c [HAIKU]** Wire DI in `ServiceCollectionExtensions.cs`; bind `TenancySettings` + `[OptionsValidator]`
  (base domain, reserved subdomains, registry connection, master-secret reference). Replace the superuser
  connection string usage.
- **Gate:** Infrastructure integration tests (Testcontainers) green; a test proves the factory connects as
  `t_<id>` and RLS confines it; the registry path works with no tenant context.

### Phase 3 — Application  (parallel-safe after Phase 2)
- **3a [SONNET]** `OAuth.cs`: add `tenant` claim (= tenant id); `LoginCommand` authenticates within the
  host-resolved tenant; request-time guard rejects `tenant` claim ≠ host tenant (403).
- **3b [SONNET]** Signed links: issue against `https://<slug>.booqr.dk`; reject when host-resolved tenant ≠
  target user's tenant (belt-and-braces on top of RLS). Keep ETag as concurrency-only.
- **3c [SONNET]** Background/scheduled work: `ActivityBackgroundService` stamps/consumes `Activity.tenant_id`
  and inserts via `booqr_batch`; reminder + `FlushTokenService` run cross-tenant as `booqr_batch`
  (BYPASSRLS) — one query, no per-tenant loop. Ensure a non-BYPASSRLS role can't run these.
- **Gate:** Application unit tests (Moq) green; transaction lifecycle asserted; unauthorized paths assert
  `Times.Never` on repositories.

### Phase 4 — API  (parallel-safe after Phase 3)
- **4a [SONNET]** Tenant-resolution middleware in `ConfigureMiddleware` (before `UseAuthorization`); populate
  scoped `ITenantContext` from `Request.Host` via `TenantRepository`; reserved/apex → no tenant;
  tenant-required endpoints reject empty context.
- **4b [HAIKU]** Anonymous `GET /api/my-tenant` endpoint in `Routing.cs` → branding or `404 tenant-not-found`
  ProblemDetails.
- **4c [SONNET]** `Program.cs` **admin-mode branch** (flag-selected) running provision/migrate/deprovision/
  rotate then exiting; ensure the web host path is untouched when the flag is absent.
- **Gate:** Api integration tests (WebApplicationFactory) green, including the isolation & `GET /api/my-tenant`
  cases.

### Phase 5 — Admin CLI commands + compose  **[SONNET]** (after Phase 4)
`SchemaMigrator` already exists (Phase 0). Here: the admin-mode commands and packaging.
- Provisioning (`INSERT public.tenants` → `CREATE ROLE t_<id> … NOBYPASSRLS` → `GRANT booqr_tenant` →
  `ALTER ROLE … SET search_path = app` → seed initial admin + tenant-bound activation link; **refuse an
  existing role**), `--deprovision`, `--rotate` — per `docs/1-design.md` §2b.
- New `admin` service in `redist/docker-compose.yml`: same image, admin mode, internal network only, holds
  `booqr_migrator`/`booqr_batch` creds; **not** on the HAProxy `api` backend. API container gets only the
  registry role + master secret.
- **Gate:** provision → migrate → isolation e2e passes end to end.

### Phase 6 — HAProxy + docs  **[HAIKU]** (parallel with Phase 5)
- `redist/web-gateway/haproxy.cfg`: exact apex redirect (naked `booqr.dk`→`www` only); accept `*.booqr.dk`;
  route `/api/`→api else app via exact/suffix matches; `default_backend deny` retained.
- `AllowedHosts` → `*.booqr.dk`; ignore client `X-Forwarded-Host`.

## Security review checklist (OWASP-aligned) — orchestrator verifies before final commit

- **A01 Broken Access Control / tenant isolation:** every `app` table has `ENABLE`+`FORCE` RLS with the
  `tenant_of(current_user)` policy; the coverage guard passes; a cross-tenant `SELECT`/`INSERT`/`UPDATE` as a
  tenant role is rejected by the DB (test proves it). No endpoint bypasses tenant context.
- **A01/A07 auth:** `tenant` claim mismatch → 403; signed links can't be replayed cross-tenant; refresh-token
  family logic unchanged and per-tenant.
- **A02 crypto/secrets:** `master_secret` only in the API + admin env, never logged; derived passwords never
  logged; no secrets in code or committed config.
- **A03 injection:** role/schema names are integer-derived (`t_<id>`) — never interpolate a raw slug into
  SQL/DDL; slug validated as a DNS label; all data access stays parameterized / generated.
- **Least privilege:** API holds registry + tenant roles only (no `CREATE`/`CREATEROLE`/`BYPASSRLS`); tenant
  roles are `NOBYPASSRLS` and own nothing; migrator/batch live only in the admin container.
- **Simplification/bloat pass:** no dead abstractions; reuse `IConnectionProvider`/`ITransaction`/existing
  command patterns; no `WHERE tenant_id=` bolted onto queries RLS already covers.

## Definition of done

- `dotnet publish` (Native AOT) succeeds **and the produced binary runs** (build alone is insufficient).
- Full `tests/` suite green under Testcontainers, including every failure case in `docs/1-design.md` §5.
- Linters/formatters clean; conventional commits; pushed to `claude/multi-tenancy-options-fsx16n`.
- No feature added beyond the design. Open questions raised to the human, not guessed.
