using System.Text;
using System.Text.Unicode;
using Klinkby.Booqr.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     Optional stand-in for <see cref="ITenantRepository" />, letting tests resolve tenants
///     deterministically without a live registry database (the fixture's connection strings point
///     at an unreachable <c>postgres:5432</c> host). When <c>null</c> (the default), the real
///     registry-backed repository is used and any attempt to reach it fails as it does today.
/// </summary>
internal sealed class WebApiFixture(
    string? allowedHosts = null,
    bool withThrowingEndpoint = false,
    ITenantRepository? tenantRepository = null)
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
            "MailClientApiKey": "...:...",
            "MailClientAccount": "1.....smtp",
            "MailClientFromAddress": "no-reply@booqr.dk",
            "Tenancy": {
              "BaseDomain": "booqr.dk",
              "ReservedSubdomains": [ "www", "status", "mta-sts" ]
            },
            "TenantDataSources": {
              "BaseConnectionString": "Host=postgres:5432;Database=postgres",
              "MasterSecret": "test-master-secret",
              "MaxPoolSize": 3,
              "MaxCacheEntries": 64
            },
            "Registry": {
              "ConnectionString": "Host=postgres:5432;Database=postgres",
              "RegistryUsername": "booqr_registry",
              "RegistryPassword": "test-registry-password",
              "CacheTtl": "00:00:30",
              "CacheSize": 256
            },
            "Batch": {
              "ConnectionString": "Host=postgres:5432;Database=postgres",
              "BatchUsername": "booqr_batch",
              "BatchPassword": "test-batch-password"
            }
          }
        }
        """;
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(jsonConfig));
        IConfigurationBuilder configurationBuilder = new ConfigurationBuilder()
            .AddJsonStream(stream);
        // In-memory WebApplicationFactory requests default to the "localhost" host, which the
        // production appsettings.json AllowedHosts (*.booqr.dk) would reject. Default the test host
        // filter to allow-all; tests that specifically exercise host filtering pass an explicit value.
        configurationBuilder.AddInMemoryCollection(
            new Dictionary<string, string?> { ["AllowedHosts"] = allowedHosts ?? "*" });

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

        if (tenantRepository is not null)
        {
            // Overrides the registry-backed ITenantRepository (which would otherwise try to reach
            // the unreachable postgres:5432 host above) so tenant-resolution middleware and the
            // GET /api/my-tenant endpoint resolve deterministically in-process.
            builder.ConfigureTestServices(services =>
                services.AddScoped(_ => tenantRepository));
        }

        if (!withThrowingEndpoint && tenantRepository is null)
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
