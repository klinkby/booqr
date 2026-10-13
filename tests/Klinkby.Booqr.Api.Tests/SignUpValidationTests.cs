using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Klinkby.Booqr.Api.Tests;

public class SignUpValidationTests
{
    [Theory]
    [InlineData("""{"email":"user@example.com"}""")]
    [InlineData("""{"email":"user@example.com","name":" ","phone":""}""")]
    public async Task GIVEN_MissingNameAndPhone_WHEN_Post_THEN_BadRequestNamesBoth(string json)
    {
        // Arrange
        await using WebApiFixture fixture = new();
        HttpClient client = fixture.CreateClient();
        using StringContent body = new(json, Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await client.PostAsync(new Uri("api/users", UriKind.Relative), body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(problem.Errors.Keys, k => k.Equals("name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(problem.Errors.Keys, k => k.Equals("phone", StringComparison.OrdinalIgnoreCase));
    }
}
