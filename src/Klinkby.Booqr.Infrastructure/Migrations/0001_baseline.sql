-- Baseline schema migration. Applied by SchemaMigrator, connected as
-- booqr_migrator, inside one transaction under an advisory lock. Recreates
-- every Booqr business table (previously in redist/initdb.sql, schema
-- `public`) under schema `app`, preserving all existing column
-- names/types/constraints/indexes, plus the multi-tenancy additions
-- specified in docs/1-design.md:
--   - tenant_id int NOT NULL DEFAULT app.tenant_of(current_user) on every table
--   - bigint promotion: bookings.id, calendar.id (+ calendar.bookingid FK)
--   - no_overlapping_events extended with tenant_id
--   - users unique -> (tenant_id, email) partial WHERE deleted IS NULL
--   - views recreated under app
--   - ENABLE + FORCE ROW LEVEL SECURITY + tenant_isolation policy on every table
--
-- This script is pure DDL — it does not write public.schema_migrations;
-- SchemaMigrator records the applied version after this script succeeds.

-- Note: the btree_gist extension (needed by the no_overlapping_events GiST
-- constraint below) is created in the control plane (initdb, as superuser),
-- because CREATE EXTENSION requires superuser and the migrator is not one.

-------------------------------------------------------------

create table app.users
(
    id           integer generated always as identity
        constraint users_pk
            primary key,
    tenant_id    int                      not null default app.tenant_of(current_user),
    email        varchar(255)             not null,
    passwordhash char(60) collate "POSIX",
    role         varchar(20)              not null,
    name         varchar(255),
    phone        bigint,
    created      timestamp with time zone not null,
    modified     timestamp with time zone not null,
    deleted      timestamp with time zone
);

create unique index idx_users_tenant_email
    on app.users (tenant_id, email)
    where (deleted is null);

create index idx_users_email
    on app.users (email)
    where (deleted is null);

create table app.locations
(
    id        integer generated always as identity
        constraint locations_pk
            primary key,
    tenant_id int                      not null default app.tenant_of(current_user),
    name      varchar(255)             not null,
    address1  varchar(255),
    address2  varchar(255),
    zip       varchar(20),
    city      varchar(255),
    created   timestamp with time zone not null,
    modified  timestamp with time zone not null,
    deleted   timestamp with time zone
);

create table app.services
(
    id          integer generated always as identity
        constraint services_pk
            primary key,
    tenant_id   int                      not null default app.tenant_of(current_user),
    name        varchar(255)             not null,
    duration    interval                 not null,
    description varchar(2000),
    created     timestamp with time zone not null,
    modified    timestamp with time zone not null,
    deleted     timestamp with time zone
);

create table app.bookings
(
    id         bigint generated always as identity
        constraint bookings_pk
            primary key,
    tenant_id  int                      not null default app.tenant_of(current_user),
    customerid integer                  not null
        constraint bookings_users_id_fk
            references app.users,
    serviceid  integer                  not null
        constraint bookings_services_id_fk
            references app.services,
    notes      varchar(2000),
    created    timestamp with time zone not null,
    modified   timestamp with time zone not null,
    deleted    timestamp with time zone
);

create table app.calendar
(
    id         bigint generated always as identity
        primary key,
    tenant_id  int                      not null default app.tenant_of(current_user),
    employeeid integer                  not null
        references app.users,
    starttime  timestamp with time zone not null,
    endtime    timestamp with time zone not null,
    locationid integer                  not null
        constraint calendar_locations_id_fk
            references app.locations,
    bookingid  bigint
        constraint calendar_bookings_id_fk
            references app.bookings,
    created    timestamp with time zone not null,
    modified   timestamp with time zone not null,
    deleted    timestamp with time zone,
    constraint no_overlapping_events
        exclude using gist (tenant_id with =, employeeid with =, tstzrange(starttime, endtime) with &&)
        where ( deleted is null ),
    constraint valid_time_range
        check (endtime > starttime)
);

create index idx_calendar_time_range
    on app.calendar (starttime desc, endtime desc)
    where (deleted is null);

create index bookings_customerid_index
    on app.bookings (customerid);

create table app.employeeservices
(
    tenant_id  int     not null default app.tenant_of(current_user),
    employeeid integer not null
        constraint employeeservices_users_id_fk
            references app.users,
    serviceid  integer not null
        constraint employeeservices_services_id_fk
            references app.services,
    constraint employeeservices_pk
        primary key (serviceid, employeeid)
);

create table app.activities
(
    id        bigint generated always as identity,
    tenant_id int                      not null default app.tenant_of(current_user),
    timestamp timestamp with time zone not null,
    requestid char(23),
    userid    integer                  not null,
    entity    varchar(20)              not null,
    entityid  integer                  not null,
    action    varchar(30)              not null,
    primary key (timestamp, id)
);

create table app.refreshtokens
(
    hash       char(40) collate "POSIX" not null,
    tenant_id  int                      not null default app.tenant_of(current_user),
    family     uuid                     not null,
    userid     integer                  not null
        constraint refreshtokens_users_id_fk
            references app.users,
    expires    timestamp with time zone not null,
    created    timestamp with time zone not null,
    revoked    timestamp with time zone,
    replacedby char(40) collate "POSIX"
        constraint refreshtokens_refreshtokens_hash_fk
            references app.refreshtokens,
    primary key (hash),
    -- Prevent a token from replacing itself
    constraint check_not_self_replaced
        check (replacedby <> hash),
    -- Prevent dangling replaced token
    constraint check_revoked_if_replaced
        check (replacedby is null or revoked is not null)
);

create index idx_refreshtokens_family
    on app.refreshtokens (family);

create index idx_refreshtokens_expires
    on app.refreshtokens (expires);

-------------------------------------------------------------

-- Cluster-wide job-claim coordination, NOT tenant data: rows are written only by the
-- booqr_batch (BYPASSRLS) role via IJobClaim (see ScheduledBackgroundService). It therefore
-- carries no tenant_id and no RLS policy - app.tenant_of('booqr_batch') is NULL, so a
-- tenant_id DEFAULT would violate NOT NULL on insert.
create table app.scheduled_job_executions
(
    job_name       varchar(50)              not null,
    execution_date date                     not null,
    claimed_at     timestamp with time zone not null,
    primary key (job_name, execution_date)
);

-------------------------------------------------------------

create view app.mybookings
            (id, starttime, endtime, customerid, serviceid, locationid, employeeid, hasnote, created, modified,
             deleted) as
select b.id,
       c.starttime,
       c.endtime,
       b.customerid,
       b.serviceid,
       c.locationid,
       c.employeeid,
       not b.notes is null and length(b.notes) > 0 as hasnote,
       b.created,
       b.modified,
       b.deleted
from app.bookings b
         join app.calendar c on b.id = c.bookingid
order by c.starttime;

create view app.bookingdetails
            (id, starttime, service, duration, location, employee, customerid, customername, customeremail, created,
             modified, deleted)
as
select b.id,
       v.starttime,
       s.name  as service,
       s.duration,
       l.name  as location,
       e.name  as employee,
       c.id    as customerid,
       c.name  as customername,
       c.email as customeremail,
       b.created,
       b.modified,
       b.deleted
from app.bookings b
         join app.calendar v on b.id = v.bookingid
         join app.locations l on v.locationid = l.id
         join app.services s on b.serviceid = s.id
         left join app.users e on v.employeeid = e.id
         left join app.users c on b.customerid = c.id
order by v.starttime;

-------------------------------------------------------------
-- Row-level security: every `app` table gets FORCE RLS + a tenant_isolation
-- policy keyed on the unforgeable current_user -> tenant_of() mapping.
-- Views are excluded (RLS applies to their underlying base tables).
-------------------------------------------------------------

alter table app.users enable row level security;
alter table app.users force row level security;
create policy tenant_isolation on app.users
    using (tenant_id = app.tenant_of(current_user))
    with check (tenant_id = app.tenant_of(current_user));

alter table app.locations enable row level security;
alter table app.locations force row level security;
create policy tenant_isolation on app.locations
    using (tenant_id = app.tenant_of(current_user))
    with check (tenant_id = app.tenant_of(current_user));

alter table app.services enable row level security;
alter table app.services force row level security;
create policy tenant_isolation on app.services
    using (tenant_id = app.tenant_of(current_user))
    with check (tenant_id = app.tenant_of(current_user));

alter table app.bookings enable row level security;
alter table app.bookings force row level security;
create policy tenant_isolation on app.bookings
    using (tenant_id = app.tenant_of(current_user))
    with check (tenant_id = app.tenant_of(current_user));

alter table app.calendar enable row level security;
alter table app.calendar force row level security;
create policy tenant_isolation on app.calendar
    using (tenant_id = app.tenant_of(current_user))
    with check (tenant_id = app.tenant_of(current_user));

alter table app.employeeservices enable row level security;
alter table app.employeeservices force row level security;
create policy tenant_isolation on app.employeeservices
    using (tenant_id = app.tenant_of(current_user))
    with check (tenant_id = app.tenant_of(current_user));

alter table app.activities enable row level security;
alter table app.activities force row level security;
create policy tenant_isolation on app.activities
    using (tenant_id = app.tenant_of(current_user))
    with check (tenant_id = app.tenant_of(current_user));

alter table app.refreshtokens enable row level security;
alter table app.refreshtokens force row level security;
create policy tenant_isolation on app.refreshtokens
    using (tenant_id = app.tenant_of(current_user))
    with check (tenant_id = app.tenant_of(current_user));

-- app.scheduled_job_executions intentionally has no RLS: it is cluster-wide coordination
-- state written only by the booqr_batch role (see table definition above), not tenant data.
