using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AutoFixture.Xunit3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Klinkby.Booqr.Api.Tests;

public class ProblemDetailsCustomizerTests
{
    [Theory]
    [AutoData]
    public async Task GIVEN_InvalidSignUpEmail_WHEN_Post_THEN_ValidationFailureLoggedWithoutValue(string localPart)
    {
        // No '@': rejected by the email regex.
        string email = $"{localPart}.example.com";
        using CapturingLoggerProvider logs = new();
        await using WebApiFixture fixture = new();
        HttpClient client = fixture
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<ILoggerProvider>(logs)))
            .CreateClient();
        using StringContent body = new($$"""{"email":"{{email}}"}""", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync(new Uri("api/users", UriKind.Relative), body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        string entry = Assert.Single(logs.Entries, static e => e.EventId == 4).Message;
        Assert.Contains("POST /api/users", entry, StringComparison.Ordinal);
        Assert.Contains("Email is not valid", entry, StringComparison.Ordinal);
        Assert.DoesNotContain(email, entry, StringComparison.Ordinal);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<(int EventId, string Message)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => this;

        public void Dispose()
        {
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue((eventId.Id, formatter(state, exception)));
    }
}
