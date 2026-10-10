-- Views become security_invoker so RLS is evaluated for the querying role:
-- an owner-privileged view applies it as booqr_migrator, hiding every row from
-- booqr_batch (the reminder job). bookingdetails also gains tenantid (appended,
-- as CREATE OR REPLACE VIEW requires) so the job can look up the tenant name.

alter view app.mybookings set (security_invoker = true);

create or replace view app.bookingdetails
            (id, starttime, service, duration, location, employee, customerid, customername, customeremail, created,
             modified, deleted, tenantid)
    with (security_invoker = true)
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
       b.deleted,
       b.tenant_id as tenantid
from app.bookings b
         join app.calendar v on b.id = v.bookingid
         join app.locations l on v.locationid = l.id
         join app.services s on b.serviceid = s.id
         left join app.users e on v.employeeid = e.id
         left join app.users c on b.customerid = c.id
order by v.starttime;
