using Klinkby.Booqr.Core;
using Klinkby.Booqr.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Klinkby.Booqr.Infrastructure.Tests;

[Collection(nameof(ServiceProviderFixture))]
public sealed class RefreshTokenRepositoryTests(ServiceProviderFixture serviceProvider)
{
    private readonly IRefreshTokenRepository _sut = serviceProvider.Services.GetRequiredService<IRefreshTokenRepository>();
    private readonly IUserRepository _userRepository = serviceProvider.Services.GetRequiredService<IUserRepository>();
    private readonly ITransaction _transaction = serviceProvider.Services.GetRequiredService<ITransaction>();

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_RefreshToken_WHEN_Add_THEN_CanBeReadBack(User user, RefreshToken token)
    {
        await _transaction.Begin();
        try
        {
            var userId = await _userRepository.Add(user);
            var tokenToAdd = token with { Hash = token.Hash[..40], UserId = userId, Revoked = null, ReplacedBy = null };

            await _sut.Add(tokenToAdd);
            var actual = await _sut.GetByHash(tokenToAdd.Hash);

            Assert.NotNull(actual);
            Assert.Equal(tokenToAdd, actual);
        }
        finally
        {
            await _transaction.Rollback();
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_RefreshToken_WHEN_RevokeSingle_THEN_IsRevoked(User user, RefreshToken token, DateTime revokeTime)
    {
        await _transaction.Begin();
        try
        {
            var userId = await _userRepository.Add(user);
            var tokenToAdd = token with { Hash = token.Hash[..40], UserId = userId, Revoked = null, ReplacedBy = null };
            await _sut.Add(tokenToAdd);

            var result = await _sut.RevokeSingle(tokenToAdd.Hash, revokeTime);
            var actual = await _sut.GetByHash(tokenToAdd.Hash);

            Assert.True(result);
            Assert.Equal(revokeTime.ToUniversalTime(), actual?.Revoked?.ToUniversalTime());
        }
        finally
        {
            await _transaction.Rollback();
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_MultipleTokensInFamily_WHEN_RevokeAll_THEN_AllAreRevoked(User user, Guid family, RefreshToken token1, RefreshToken token2, DateTime revokeTime)
    {
        await _transaction.Begin();
        try
        {
            var userId = await _userRepository.Add(user);
            var t1 = token1 with { Hash = token1.Hash[..40], UserId = userId, Family = family, Revoked = null, ReplacedBy = null };
            var t2 = token2 with { Hash = token2.Hash[..40], UserId = userId, Family = family, Revoked = null, ReplacedBy = null };

            await _sut.Add(t1);
            await _sut.Add(t2);

            var revokedCount = await _sut.RevokeAll(family, revokeTime);
            var actual1 = await _sut.GetByHash(t1.Hash);
            var actual2 = await _sut.GetByHash(t2.Hash);

            Assert.Equal(2, revokedCount);
            Assert.Equal(revokeTime.ToUniversalTime(), actual1?.Revoked?.ToUniversalTime());
            Assert.Equal(revokeTime.ToUniversalTime(), actual2?.Revoked?.ToUniversalTime());
        }
        finally
        {
            await _transaction.Rollback();
        }
    }

    [Theory]
    [IntegrationAutoData]
    public async Task GIVEN_ExpiredTokens_WHEN_Delete_THEN_AreRemoved(User user, RefreshToken token, DateTime now)
    {
        await _transaction.Begin();
        try
        {
            var userId = await _userRepository.Add(user);
            var expiredToken = token with { Hash = token.Hash[..40], UserId = userId, Expires = now.AddMinutes(-1), Revoked = null, ReplacedBy = null };
            await _sut.Add(expiredToken);

            var deletedCount = await _sut.Delete(now);
            var actual = await _sut.GetByHash(expiredToken.Hash);

            Assert.Equal(1, deletedCount);
            Assert.Null(actual);
        }
        finally
        {
            await _transaction.Rollback();
        }
    }
}
