using System.Collections.Specialized;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Application.Tests;

public class ExpiringQueryStringTests
{
    private static IOptions<PasswordSettings> PasswordSettings => Options
        .Create(new PasswordSettings
        {
            HmacKey = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(
                    HMACSHA3_384.HashSizeInBytes))
        });
    private readonly FakeTimeProvider _timeProvider = new();

    [Fact]
    public void BuildAndValidate_ShouldReturnTrue_ForValidQueryString()
    {
        // Arrange
        ExpiringQueryString sut = new(PasswordSettings, _timeProvider);

        // Act
        var queryString = sut.Create(TimeSpan.FromHours(1));
        var isValid = sut.TryParse(queryString, out NameValueCollection? _, out QueryStringValidation _);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [AutoData]
    public void BuildAndValidate_ShouldReturnTrue_ForValidAdditionalParameters(string action, int userId)
    {
        // Arrange
        ExpiringQueryString sut = new(PasswordSettings, _timeProvider);
        NameValueCollection inParameters = new()
        {
            { nameof(action), action },
            { nameof(userId), userId.ToString(CultureInfo.InvariantCulture) }
        };

        // Act
        var queryString = sut.Create(TimeSpan.FromHours(1), inParameters);
        var isValid = sut.TryParse(queryString, out NameValueCollection? outParameters, out QueryStringValidation _);

        // Assert
        Assert.True(isValid);
        Assert.NotNull(outParameters);
        Assert.Equal(inParameters[nameof(action)], outParameters[nameof(action)]);
        Assert.Equal(inParameters[nameof(userId)], outParameters[nameof(userId)]);
        Assert.NotNull(outParameters["expires"]);
    }

    [Theory]
    [InlineData("1999")]
    [InlineData("foo")]
    public void BuildAndValidate_ShouldReturnFalse_ForTamperedExpiry(string replacement)
    {
        // Arrange
        ExpiringQueryString sut = new(PasswordSettings, _timeProvider);

        // Act
        var queryString = sut
            .Create(TimeSpan.FromHours(1))
            .Replace("2000", replacement, StringComparison.Ordinal);
        var isValid = sut.TryParse(queryString, out NameValueCollection? _, out QueryStringValidation _);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void BuildAndValidate_ShouldReturnFalse_ForExpiredLink()
    {
        // Arrange
        ExpiringQueryString sut = new(PasswordSettings, _timeProvider);
        _timeProvider.Advance(TimeSpan.FromMinutes(61));

        // Act
        var queryString = sut.Create(TimeSpan.Zero);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        var isValid = sut.TryParse(queryString, out NameValueCollection? _, out QueryStringValidation _);

        // Assert
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("&hash=invalidhash")]
    [InlineData("&hash=")]
    [InlineData("")]
    public void BuildAndValidate_ShouldReturnFalse_ForNoHash(string replacement)
    {
        // Arrange
        ExpiringQueryString sut = new(PasswordSettings, _timeProvider);

        // Act
        var queryString = sut.Create(TimeSpan.FromHours(1))
                             .Split("&hash=")[0]
                          + replacement;
        var isValid = sut.TryParse(queryString, out NameValueCollection? _, out QueryStringValidation _);

        // Assert
        Assert.False(isValid);
    }
}
