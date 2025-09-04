create extension btree_gist
    schema public
    version '1.6';

comment on extension btree_gist is 'support for indexing common datatypes in GiST';

---

create table public.users
(
    id           integer generated always as identity
        constraint users_pk
            primary key,
    email        varchar(255)             not null
        constraint users_email
            unique,
    passwordhash varchar(255)             not null,
    role         varchar(20)              not null,
    name         varchar(255),
    phone        bigint,
    created      timestamp with time zone not null,
    modified     timestamp with time zone not null,
    deleted      timestamp with time zone
);

alter table public.users
    owner to postgres;

create index idx_users_email
    on public.users (email)
    where (deleted IS NULL);

create table public.locations
(
    id       integer generated always as identity
        constraint locations_pk
            primary key,
    name     varchar(255)             not null,
    address1 varchar(255),
    address2 varchar(255),
    zip      varchar(20),
    city     varchar(255),
    created  timestamp with time zone not null,
    modified timestamp with time zone not null,
    deleted  timestamp with time zone
);

alter table public.locations
    owner to postgres;

create table public.services
(
    id       integer generated always as identity
        constraint services_pk
            primary key,
    name     varchar(255)             not null,
    duration interval                 not null,
    created  timestamp with time zone not null,
    modified timestamp with time zone not null,
    deleted  timestamp with time zone
);

alter table public.services
    owner to postgres;

create table public.bookings
(
    id             integer generated always as identity
        constraint bookings_pk
            primary key,
    customerid     integer                  not null
        constraint bookings_users_id_fk
            references public.users,
    "serviceid   " integer                  not null
        constraint bookings_services_id_fk
            references public.services,
    notes          text,
    created        timestamp with time zone not null,
    modified       timestamp with time zone not null,
    deleted        timestamp with time zone
);

alter table public.bookings
    owner to postgres;

create table public.calendar
(
    id         integer generated always as identity
        primary key,
    employeeid integer                  not null
        references public.users,
    starttime  timestamp with time zone not null,
    endtime    timestamp with time zone not null,
    locationid integer                  not null
        constraint calendar_locations_id_fk
            references public.locations,
    bookingid  integer
        constraint calendar_bookings_id_fk
            references public.bookings,
    created    timestamp with time zone not null,
    modified   timestamp with time zone not null,
    deleted    timestamp with time zone,
    constraint no_overlapping_events
        exclude using gist (employeeid with =, tstzrange(starttime, endtime) with &&)
        where ( deleted is null ),
    constraint valid_time_range
        check (endtime > starttime)
);

alter table public.calendar
    owner to postgres;

create index idx_calendar_time_range
    on public.calendar (starttime desc, endtime desc)
    where (deleted IS NULL);

create index bookings_customerid_index
    on public.bookings (customerid);

create table public.employeeservices
(
    employeeid integer not null
        constraint employeeservices_users_id_fk
            references public.users,
    serviceid  integer not null
        constraint employeeservices_services_id_fk
            references public.services,
    constraint employeeservices_pk
        primary key (serviceid, employeeid)
);

alter table public.employeeservices
    owner to postgres;
