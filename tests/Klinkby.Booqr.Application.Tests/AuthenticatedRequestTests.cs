using Klinkby.Booqr.Application.Abstractions;

namespace Klinkby.Booqr.Application.Tests;

public class AuthenticatedRequestTests
{
    private sealed record TestRequest : AuthenticatedRequest;

    [Fact]
    public void GIVEN_NameIdentifierClaim_WHEN_AuthenticatedUserId_THEN_ReturnsId()
    {
        var request = new TestRequest { User = CreateUser(7) };

        Assert.Equal(7, request.AuthenticatedUserId);
    }

    [Fact]
    public void GIVEN_OnlySubClaim_WHEN_AuthenticatedUserId_THEN_ReturnsId()
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim("sub", "9"));
        var request = new TestRequest { User = new ClaimsPrincipal(identity) };

        Assert.Equal(9, request.AuthenticatedUserId);
    }

    [Fact]
    public void GIVEN_NoIdentityClaim_WHEN_AuthenticatedUserId_THEN_ThrowsUnauthorized()
    {
        var request = new TestRequest { User = new ClaimsPrincipal(new ClaimsIdentity("Test")) };

        Assert.Throws<UnauthorizedAccessException>(() => request.AuthenticatedUserId);
    }

    [Fact]
    public void GIVEN_NullUser_WHEN_AuthenticatedUserId_THEN_ThrowsUnauthorized()
    {
        var request = new TestRequest();

        Assert.Throws<UnauthorizedAccessException>(() => request.AuthenticatedUserId);
    }

    [Fact]
    public void GIVEN_NonNumericClaim_WHEN_AuthenticatedUserId_THEN_ThrowsUnauthorized()
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "not-a-number"));
        var request = new TestRequest { User = new ClaimsPrincipal(identity) };

        Assert.Throws<UnauthorizedAccessException>(() => request.AuthenticatedUserId);
    }
}
