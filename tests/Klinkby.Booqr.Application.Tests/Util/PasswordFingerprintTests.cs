using Klinkby.Booqr.Application.Util;

namespace Klinkby.Booqr.Application.Tests.Util;

public class PasswordFingerprintTests
{
    [Theory]
    [ApplicationAutoData]
    public void GIVEN_SameInputs_WHEN_Compute_THEN_Deterministic(string hash)
    {
        var key = NewHmacKey();
        Assert.Equal(PasswordFingerprint.Compute(hash, key), PasswordFingerprint.Compute(hash, key));
    }

    [Theory]
    [ApplicationAutoData]
    public void GIVEN_DifferentHashes_WHEN_Compute_THEN_Differ(string hash1, string hash2)
    {
        var key = NewHmacKey();
        Assert.NotEqual(PasswordFingerprint.Compute(hash1, key), PasswordFingerprint.Compute(hash2, key));
    }

    [Theory]
    [ApplicationAutoData]
    public void GIVEN_DifferentKeys_WHEN_Compute_THEN_Differ(string hash)
    {
        Assert.NotEqual(PasswordFingerprint.Compute(hash, NewHmacKey()), PasswordFingerprint.Compute(hash, NewHmacKey()));
    }

    [Theory]
    [ApplicationAutoData]
    public void GIVEN_Hash_WHEN_Compute_THEN_20Base64UrlCharsWithoutHash(string hash)
    {
        var pv = PasswordFingerprint.Compute(hash, NewHmacKey());

        Assert.Equal(20, pv.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", pv);
        Assert.DoesNotContain(hash, pv, StringComparison.Ordinal);
    }

    [Theory]
    [ApplicationAutoData]
    public void GIVEN_MatchingFingerprint_WHEN_Verify_THEN_True(string hash)
    {
        var key = NewHmacKey();
        Assert.True(PasswordFingerprint.Verify(PasswordFingerprint.Compute(hash, key), hash, key));
    }

    [Theory]
    [ApplicationAutoData]
    public void GIVEN_OtherHashOrKey_WHEN_Verify_THEN_False(string hash, string otherHash)
    {
        var key = NewHmacKey();
        var pv = PasswordFingerprint.Compute(hash, key);

        Assert.False(PasswordFingerprint.Verify(pv, otherHash, key));
        Assert.False(PasswordFingerprint.Verify(pv, hash, NewHmacKey()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GIVEN_NullOrEmptyCandidate_WHEN_Verify_THEN_False(string? candidate)
    {
        Assert.False(PasswordFingerprint.Verify(candidate, "hash", NewHmacKey()));
    }
}
