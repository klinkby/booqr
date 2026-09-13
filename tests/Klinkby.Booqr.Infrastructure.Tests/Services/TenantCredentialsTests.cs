using Klinkby.Booqr.Infrastructure.Services;

namespace Klinkby.Booqr.Infrastructure.Tests.Services;

/// <summary>
///     Known-answer test for <see cref="TenantCredentials.DerivePassword" />. This unit test runs
///     with no container (in-sandbox) and pins the exact contract Phase 5 (admin CLI provisioning)
///     must reproduce byte-for-byte: message = tenant id as an invariant-culture decimal ASCII
///     string, MAC = HMAC-SHA384(UTF-8 master secret, message), encoding = base64url (RFC 4648 §5,
///     unpadded).
/// </summary>
public sealed class TenantCredentialsTests
{
    // Known-answer vector. Recompute with:
    //   HMAC-SHA384(key = UTF-8 "test-master-secret", message = UTF-8 "42") -> base64url, unpadded
    private const string MasterSecret = "test-master-secret";
    private const int TenantId = 42;
    private const string ExpectedPassword = "IKRGqZ1vjm7lIsC_91hyZcLmyDkVSnkz5268K_21vlSXHTrz2GJPzsoPy0TE8SDm";

    [Fact]
    public void GIVEN_KnownSecretAndTenantId_WHEN_DerivingPassword_THEN_MatchesKnownAnswerVector()
    {
        var actual = TenantCredentials.DerivePassword(MasterSecret, TenantId);

        Assert.Equal(ExpectedPassword, actual);
    }

    [Fact]
    public void GIVEN_DerivedPassword_WHEN_Inspected_THEN_ContainsNoBase64PaddingOrUnsafeUrlCharacters()
    {
        var actual = TenantCredentials.DerivePassword(MasterSecret, TenantId);

        Assert.DoesNotContain('=', actual);
        Assert.DoesNotContain('+', actual);
        Assert.DoesNotContain('/', actual);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GIVEN_DifferentTenantIds_WHEN_DerivingPassword_THEN_PasswordsDiffer(int otherTenantId)
    {
        var basePassword = TenantCredentials.DerivePassword(MasterSecret, TenantId);
        var otherPassword = TenantCredentials.DerivePassword(MasterSecret, otherTenantId);

        Assert.NotEqual(basePassword, otherPassword);
    }

    [Fact]
    public void GIVEN_SameSecretAndId_WHEN_DerivingPasswordTwice_THEN_ResultIsDeterministic()
    {
        var first = TenantCredentials.DerivePassword(MasterSecret, TenantId);
        var second = TenantCredentials.DerivePassword(MasterSecret, TenantId);

        Assert.Equal(first, second);
    }
}
