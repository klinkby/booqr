using System.Text.Json;
using AutoFixture.Xunit3;
using Klinkby.Booqr.Api.Util;
using Klinkby.Booqr.Application.Commands.Auth;
using Klinkby.Booqr.Application.Commands.Users;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     Exercises the source-generated (AOT) serializer context, so the <c>EmailJsonConverter</c> attribute is
///     proven to apply on the same path production binding uses — i.e. before validation runs.
/// </summary>
public class EmailNormalizationTests
{
    // Mirrors production: web defaults (camelCase, case-insensitive) resolved through the source-gen context.
    private static readonly JsonSerializerOptions WebOptions =
        new(JsonSerializerDefaults.Web) { TypeInfoResolver = AppJsonSerializerContext.Default };

    [Theory]
    [AutoData]
    public void GIVEN_MixedCaseEmail_WHEN_DeserializeSignUp_THEN_TrimmedLowerCase(string localPart)
    {
        string json = $$"""{"email":"  X{{localPart}}@Example.COM "}""";

        SignUpRequest? request = JsonSerializer.Deserialize<SignUpRequest>(json, WebOptions);

        AssertNormalized($"x{localPart}@example.com", request?.Email);
    }

    [Theory]
    [AutoData]
    public void GIVEN_MixedCaseEmail_WHEN_DeserializeResetPassword_THEN_TrimmedLowerCase(string localPart)
    {
        string json = $$"""{"email":"  X{{localPart}}@Example.COM "}""";

        ResetPasswordRequest? request =
            JsonSerializer.Deserialize<ResetPasswordRequest>(json, WebOptions);

        AssertNormalized($"x{localPart}@example.com", request?.Email);
    }

    [Theory]
    [AutoData]
    public void GIVEN_MixedCaseEmail_WHEN_DeserializeLogin_THEN_TrimmedLowerCaseAndPasswordUntouched(
        string localPart, string password)
    {
        string json = $$"""{"email":"  X{{localPart}}@Example.COM ","password":" {{password}}X"}""";

        LoginRequest? request = JsonSerializer.Deserialize<LoginRequest>(json, WebOptions);

        AssertNormalized($"x{localPart}@example.com", request?.Email);
        Assert.Equal($" {password}X", request?.Password);
    }

    private static void AssertNormalized(string expected, string? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected, actual, ignoreCase: true);
        Assert.DoesNotContain(actual, char.IsUpper);
    }
}
