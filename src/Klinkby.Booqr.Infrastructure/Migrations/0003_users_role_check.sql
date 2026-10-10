-- Restricts app.users.role to the known roles. Applied by SchemaMigrator,
-- connected as booqr_migrator, inside one transaction under an advisory lock.
-- Pure DDL; SchemaMigrator records the applied version afterwards.

alter table app.users
    add constraint users_role_check check (role in ('Customer', 'Employee', 'Admin'));
