-- Control plane bootstrap. Runs once, as the Postgres superuser, via
-- docker-entrypoint-initdb.d. Creates the schemas, the tenant registry,
-- the migration ledger and the four role classes described in
-- docs/1-design.md ("The RLS mechanism" / "SQL changes").
--
-- No business tables live here — those are owned by the migration path
-- (src/Klinkby.Booqr.Infrastructure/Migrations/0001_baseline.sql), applied
-- by booqr_migrator, so there is a single authoritative definition of the
-- `app` schema.

create schema app;

-- Required by the no_overlapping_events GiST exclusion constraint (which mixes
-- btree equality on tenant_id/employeeid with range overlap). CREATE EXTENSION
-- needs superuser, so it lives here in the control plane rather than in the
-- migrator-run baseline migration.
create extension if not exists btree_gist schema public;

-- Lock down public: only the migrator (schema/table owner) and superuser
-- may create objects in it; ordinary roles cannot pollute the registry
-- schema.
revoke create on schema public from public;

-------------------------------------------------------------
-- Tenant registry
-------------------------------------------------------------

create table public.tenants
(
    id           int generated always as identity
        constraint tenants_pk
            primary key,
    slug         varchar(32)              not null,
    db_role      name                     not null,
    display_name text                     not null,
    created      timestamptz              not null default now(),
    modified     timestamptz              not null default now(),
    deleted      timestamptz
);

create unique index idx_tenants_slug
    on public.tenants (slug)
    where (deleted is null);

-------------------------------------------------------------
-- Migration ledger
-------------------------------------------------------------

create table public.schema_migrations
(
    version int primary key,
    applied timestamptz not null default now()
);

-------------------------------------------------------------
-- Role classes
--
-- LOGIN roles are created with a placeholder password so the container is
-- usable out of the box; production deployments MUST rotate these via
-- `ALTER ROLE … PASSWORD …` (or an equivalent secrets-injection step) before
-- accepting traffic. Per-tenant `t_<id>` roles are created later by the
-- provisioning engine / admin CLI with a derived password — never here.
-------------------------------------------------------------

-- Resolves host -> tenant; reads public.tenants only, never a tenant
-- connection.
create role booqr_registry login password 'changeme_booqr_registry';

-- NOLOGIN group role holding the tenant grant set. Per-tenant `t_<id>`
-- roles become members of this group (see 02-provisioning-engine.sql for
-- the grants, and the admin CLI for `GRANT booqr_tenant TO t_<id>`).
create role booqr_tenant nologin;

-- Elevated: owns `app`, applies migrations, writes public.tenants and
-- provisions tenant roles. Admin container only.
create role booqr_migrator login createrole password 'changeme_booqr_migrator';

-- Cross-tenant batch/background work (reminder mail, token flush). Admin /
-- background-worker container only.
create role booqr_batch login bypassrls password 'changeme_booqr_batch';

grant usage on schema public to booqr_registry;
grant select on public.tenants to booqr_registry;

-- The migrator connects to record applied migrations in public.schema_migrations
-- and to write the tenant registry during provisioning (docs/1-design.md §1:
-- "writes public.tenants"). CREATE was revoked from PUBLIC above, so USAGE on the
-- public schema must be granted explicitly.
grant usage on schema public to booqr_migrator;
grant select, insert on public.schema_migrations to booqr_migrator;
grant select, insert, update on public.tenants to booqr_migrator;

-- Migrator owns all future `app` objects (tables/views/functions created by
-- migrations).
alter schema app owner to booqr_migrator;
