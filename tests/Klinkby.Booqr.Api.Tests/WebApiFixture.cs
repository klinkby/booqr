using System.Text;
using System.Text.Unicode;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klinkby.Booqr.Api.Tests;

internal sealed class WebApiFixture(string? allowedHosts = null, bool withThrowingEndpoint = false)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        string jsonConfig = """
        {
          "Application": {
            "Jwt": {
              "Key": "fa15a2b3982173649182736498127364192387648ad08alskdjcnlaskjdncbbdba",
              "Issuer": "booqr",
              "Audience": "https://www.booqr.dk"
            },
            "Password": {
              "HmacKey": "WzE4MiwxOTksOTQsNzcsMjU0LDY2LDQ3LDIzMyw5MywxMjcsMjUsMTIyLDU0LDE0OCwyNCwzMywxODgsNjMsMjI1LDE0Nyw5NiwyMzksMTc3LDEyMywyMjQsMTI2LDE4NywyMTUsMTY1LDEyNCwyMjQsMjM2XQ==",
              "ResetPath": "/change-password",
              "ResetTimeoutHours": 2,
              "SignUpTimeoutHours": 24
            }
          },
          "Infrastructure": {
            "ConnectionString": "Host=postgres:5432;Database=postgres;Username=postgres;Password=...",
            "MailClientApiKey": "...:...",
            "MailClientAccount": "1.....smtp",
            "MailClientFromAddress": "no-reply@booqr.dk"
          }
        }
        """;
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(jsonConfig));
        IConfigurationBuilder configurationBuilder = new ConfigurationBuilder()
            .AddJsonStream(stream);
        if (allowedHosts is not null)
        {
            configurationBuilder.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AllowedHosts"] = allowedHosts });
        }

        IConfigurationRoot configuration = configurationBuilder.Build();
        builder.UseConfiguration(configuration);
        builder.ConfigureAppConfiguration(_ => { });

        if (withThrowingEndpoint)
        {
            // Force the non-Development branch so GlobalExceptionHandler (not the developer
            // exception page) handles the throw, regardless of the ambient environment.
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(static services =>
                services.AddSingleton<IStartupFilter, ThrowingStartupFilter>());
        }
        else
        {
            builder.ConfigureTestServices(_ => { });
        }
    }

    /// <summary>
    ///     Appends a terminal middleware that always throws, placed <em>after</em> the application
    ///     pipeline so it runs downstream of <c>UseExceptionHandler</c>. Any unmatched request (e.g.
    ///     <c>GET /api/test-throw</c>) reaches it and surfaces an unhandled exception through the
    ///     real pipeline.
    /// </summary>
    private sealed class ThrowingStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Run(static _ => throw new TimeoutException("Simulated unhandled exception"));
        };
    }
}
