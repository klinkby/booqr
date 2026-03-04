namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

[Collection(nameof(ServiceProviderFixture))]
public sealed class JobClaimRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IJobClaim _sut = serviceProvider.Services.GetRequiredService<IJobClaim>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_NoClaim_WHEN_TryClaim_THEN_ReturnsTrue(string jobName, DateTime t0)
    {
        await _transaction.Begin();
        try
        {
            var today = DateOnly.FromDateTime(t0);

            var result = await _sut.TryClaimAsync(jobName, today);

            Assert.True(result);
        }
        finally
        {
            await _transaction.Rollback();
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_ExistingClaim_WHEN_TryClaimSameDate_THEN_ReturnsFalse(string jobName, DateTime t0)
    {
        await _transaction.Begin();
        try
        {
            var today = DateOnly.FromDateTime(t0);
            await _sut.TryClaimAsync(jobName, today);

            var result = await _sut.TryClaimAsync(jobName, today);

            Assert.False(result);
        }
        finally
        {
            await _transaction.Rollback();
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_ExistingClaim_WHEN_TryClaimDifferentDate_THEN_ReturnsTrue(string jobName, DateTime t0)
    {
        await _transaction.Begin();
        try
        {
            var today = DateOnly.FromDateTime(t0);
            var tomorrow = today.AddDays(1);
            await _sut.TryClaimAsync(jobName, today);

            var result = await _sut.TryClaimAsync(jobName, tomorrow);

            Assert.True(result);
        }
        finally
        {
            await _transaction.Rollback();
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_OldClaims_WHEN_DeleteOldClaims_THEN_ReturnsDeletedCount(string jobName, DateTime t0)
    {
        await _transaction.Begin();
        try
        {
            var today = DateOnly.FromDateTime(t0);
            var oldDate = today.AddDays(-40);
            var cutoff = today.AddDays(-30);
            await _sut.TryClaimAsync(jobName, oldDate);
            await _sut.TryClaimAsync(jobName, today);

            var deleted = await _sut.DeleteOldClaimsAsync(cutoff);

            Assert.Equal(1, deleted);
        }
        finally
        {
            await _transaction.Rollback();
        }
    }
}
