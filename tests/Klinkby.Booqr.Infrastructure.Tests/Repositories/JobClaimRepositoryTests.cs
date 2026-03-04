namespace Klinkby.Booqr.Infrastructure.Tests.Repositories;

[Collection(nameof(ServiceProviderFixture))]
public sealed class JobClaimRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IJobClaim _sut = serviceProvider.Services.GetRequiredService<IJobClaim>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_NoClaim_WHEN_TryClaim_THEN_ReturnsTrue(string jobName)
    {
        await _transaction.Begin();
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

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
    public async Task GIVEN_ExistingClaim_WHEN_TryClaimSameDate_THEN_ReturnsFalse(string jobName)
    {
        await _transaction.Begin();
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
    public async Task GIVEN_ExistingClaim_WHEN_TryClaimDifferentDate_THEN_ReturnsTrue(string jobName)
    {
        await _transaction.Begin();
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
    public async Task GIVEN_OldClaims_WHEN_DeleteOldClaims_THEN_ReturnsDeletedCount(string jobName)
    {
        await _transaction.Begin();
        try
        {
            var oldDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40);
            var recentDate = DateOnly.FromDateTime(DateTime.UtcNow);
            await _sut.TryClaimAsync(jobName, oldDate);
            await _sut.TryClaimAsync(jobName, recentDate);
            var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);

            var deleted = await _sut.DeleteOldClaimsAsync(cutoff);

            Assert.Equal(1, deleted);
        }
        finally
        {
            await _transaction.Rollback();
        }
    }
}
