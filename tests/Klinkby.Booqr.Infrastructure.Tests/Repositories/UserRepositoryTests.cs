using Npgsql;

namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

[Collection(nameof(ServiceProviderFixture))]
public sealed class UserRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IUserRepository _sut = serviceProvider.Services.GetRequiredService<IUserRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_User_WHEN_Add_THEN_CanBeReadBack(User expected, string name)
    {
        int newId;
        User? actual1;
        User? actual2;
        await _transaction.Begin();
        try
        {
            newId = await _sut.Add(expected);
            actual1 = await _sut.GetById(newId);
            await _sut.Patch(new PartialUser(newId) { Name = name, Version = actual1!.Version });
            await _sut.Delete(newId);
            await _sut.Undelete(newId);
            actual2 = await _sut.GetByEmail(expected.Email);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.InRange(newId, 1, int.MaxValue);
        Assert.Equal(expected with
            {
                Id = actual1!.Id,
                Created = actual1.Created,
                Modified = actual1.Modified,
                Version = actual1.Version
            },
            actual1);
        Assert.Equal(actual1.Id, actual2!.Id);
        Assert.Equal(name, actual2.Name);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_User_WHEN_GetByIdForUpdate_THEN_ReturnsUserOrNullIfDeleted(User user)
    {
        User? live;
        User? deleted;
        await _transaction.Begin();
        try
        {
            var id = await _sut.Add(user);
            live = await _sut.GetByIdForUpdate(id);
            await _sut.Delete(id);
            deleted = await _sut.GetByIdForUpdate(id);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.NotNull(live);
        Assert.Equal(user.Email, live.Email);
        Assert.Null(deleted);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_Users_WHEN_LockAdmins_THEN_ReturnsOnlyNonDeletedAdminIds(
        User admin, User deletedAdmin, User employee, User customer)
    {
        IReadOnlyList<int> actual;
        int adminId;
        int deletedAdminId;
        await _transaction.Begin();
        try
        {
            adminId = await _sut.Add(admin with { Role = UserRole.Admin });
            deletedAdminId = await _sut.Add(deletedAdmin with { Role = UserRole.Admin });
            await _sut.Add(employee with { Role = UserRole.Employee });
            await _sut.Add(customer);
            await _sut.Delete(deletedAdminId);
            actual = await _sut.LockAdmins();
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.Contains(adminId, actual);
        Assert.DoesNotContain(deletedAdminId, actual);
        Assert.Equal(actual.Order().ToList(), actual);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_User_WHEN_PatchOnlyRole_THEN_RoleChangedAndOtherFieldsIntact(User user)
    {
        User? before;
        User? after;
        bool patched;
        await _transaction.Begin();
        try
        {
            var id = await _sut.Add(user);
            before = await _sut.GetById(id);
            patched = await _sut.Patch(new PartialUser(id) { Role = UserRole.Employee, Version = before!.Version });
            after = await _sut.GetById(id);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.True(patched);
        Assert.Equal(UserRole.Employee, after!.Role);
        Assert.Equal(before.Email, after.Email);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Phone, after.Phone);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_UserWithPhone_WHEN_UpdateProfileWithNullPhone_THEN_PhoneClearedAndOtherFieldsIntact(
        User user, string name)
    {
        User? before;
        User? after;
        bool updated;
        await _transaction.Begin();
        try
        {
            var id = await _sut.Add(user with { Phone = 12345678 });
            before = await _sut.GetById(id);
            updated = await _sut.UpdateProfile(new PartialUser(id) { Name = name, Phone = null, Version = before!.Version });
            after = await _sut.GetById(id);
        }
        finally
        {
            await _transaction.Rollback();
        }

        Assert.True(updated);
        Assert.Null(after!.Phone);
        Assert.Equal(name, after.Name);
        Assert.Equal(before.Email, after.Email);
        Assert.Equal(before.Role, after.Role);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_InvalidRole_WHEN_AddOrPatch_THEN_CheckViolation(User user, User other, string invalidRole)
    {
        await _transaction.Begin();
        try
        {
            PostgresException addEx = await Assert.ThrowsAsync<PostgresException>(() =>
                _sut.Add(user with { Role = invalidRole[..10] }));
            Assert.Equal(PostgresErrorCodes.CheckViolation, addEx.SqlState);
        }
        finally
        {
            await _transaction.Rollback();
        }

        await _transaction.Begin();
        try
        {
            var id = await _sut.Add(other);
            User? existing = await _sut.GetById(id);
            PostgresException patchEx = await Assert.ThrowsAsync<PostgresException>(() =>
                _sut.Patch(new PartialUser(id) { Role = invalidRole[..10], Version = existing!.Version }));
            Assert.Equal(PostgresErrorCodes.CheckViolation, patchEx.SqlState);
        }
        finally
        {
            await _transaction.Rollback();
        }
    }
}
