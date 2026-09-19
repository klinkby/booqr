-- Provisioning engine bootstrap. Runs once, as the Postgres superuser,
-- immediately after 01-control-plane.sql. Creates app.tenant_of() — the
-- unforgeable current_user -> tenant_id mapping every RLS policy is keyed
-- on — and sets the default privileges that make every future migration
-- table/sequence auto-grant to the tenant roles.

-- SECURITY DEFINER so tenant roles (which have no direct SELECT on
-- public.tenants) can still resolve their own tenant id via current_user.
create function app.tenant_of(login_role name)
    returns int
    language sql
    stable
    security definer
as
$$
select id
from public.tenants
where db_role = login_role
  and deleted is null
$$;

revoke all on function app.tenant_of(name) from public;
grant execute on function app.tenant_of(name) to booqr_tenant, booqr_batch;

grant usage on schema app to booqr_tenant, booqr_batch;

-- Set BEFORE any table exists in `app`, so tables/sequences created later
-- by booqr_migrator (Migrations/0001_baseline.sql and onward) are
-- automatically granted to the tenant roles with no per-migration grant
-- statements needed.
alter default privileges for role booqr_migrator in schema app
    grant select, insert, update, delete on tables to booqr_tenant, booqr_batch;

alter default privileges for role booqr_migrator in schema app
    grant usage, select on sequences to booqr_tenant, booqr_batch;
