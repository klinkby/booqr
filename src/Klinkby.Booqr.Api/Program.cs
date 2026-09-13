using System.Diagnostics;
using System.Reflection;
using Klinkby.Booqr.Api;
using Klinkby.Booqr.Api.Admin;
using Klinkby.Booqr.Api.Worker;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi;
using NLog.Web;

var timer = Stopwatch.StartNew();

// Admin mode: same Native-AOT image, started with a leading "admin" argument, runs one
// provisioning/migration command against the database and exits without starting Kestrel or any
// part of the web pipeline (docs/1-design.md "2b. Admin CLI (admin mode)"). Detected before the
// WebApplicationBuilder is created so the admin path never touches tenant/registry HTTP request
// context. The actual admin commands (provision/migrate/deprovision/rotate) are Phase 5; here we
// only detect the flag and dispatch to the AdminRunner seam Phase 5 fills in.
if (args.Length > 0 && string.Equals(args[0], "admin", StringComparison.OrdinalIgnoreCase))
{
    return await AdminRunner.RunAsync(args[1..]);
}

// Worker mode: same Native-AOT image, started with a leading "worker" argument, runs the
// cross-tenant scheduled jobs (reminder mail, refresh-token flush) as the booqr_batch
// (BYPASSRLS) role on a generic host - no Kestrel, no JWT, no tenant data sources/master
// secret. See Klinkby.Booqr.Api.Worker.WorkerRunner for the security-boundary rationale.
if (args.Length > 0 && string.Equals(args[0], "worker", StringComparison.OrdinalIgnoreCase))
{
    return await WorkerRunner.RunAsync(args[1..]);
}

// Or API mode:
WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

// Detect if running in OpenAPI document generation mode
// https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi
var isMockServer = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

ConfigureLogging(builder, isMockServer);
ConfigureServices(builder, isMockServer);

WebApplication app = builder.Build();
ConfigureMiddleware(app, isMockServer);
ConfigureEndpoints(app);

await RunApplicationAsync(app, timer);
return 0;

static void ConfigureLogging(WebApplicationBuilder builder, bool isMockServer)
{
    if (isMockServer)
    {
        return;
    }

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();
}

static void ConfigureServices(WebApplicationBuilder builder, bool isMockServer)
{
    ConfigurationManager configuration = builder.Configuration;
    builder.Services
        .AddApplication(configuration.GetRequiredSection("Application"), isMockServer)
        .AddApi(configuration.GetRequiredSection("Application:Jwt"));

    if (isMockServer)
    {
        builder.Services.AddOpenApi(ConfigureBearerAuthentication);
    }
    else
    {
        builder.Services
            .AddSingleton<TimeProvider>(static _ => TimeProvider.System)
            .AddTenancy(configuration.GetRequiredSection("Infrastructure:Tenancy"))
            .AddApiInfrastructure(configuration.GetRequiredSection("Infrastructure"));

        // CreateSlimBuilder omits the default host-filtering startup filter, so wire it
        // explicitly. The app emits its own authority (e.g. password-reset links) from the
        // Host header, so constrain it here as defense-in-depth instead of trusting the proxy
        // alone. An unset or "*" value allows all hosts (dev); production sets the real host.
        builder.Services.Configure<HostFilteringOptions>(options =>
        {
            var allowedHosts = configuration["AllowedHosts"];
            if (!string.IsNullOrWhiteSpace(allowedHosts) && allowedHosts != "*")
            {
                options.AllowedHosts = allowedHosts
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        });
        builder.WebHost
            .UseKestrelCore()
            .UseUrls()
            .ConfigureKestrel(options =>
            {
                // Optional Unix socket endpoint (typically for container/proxy scenarios) supporting H2C
                var unixSocketPath = configuration["Kestrel:UnixSocketPath"];
                if (!string.IsNullOrWhiteSpace(unixSocketPath))
                {
                    options.ListenUnixSocket(unixSocketPath, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                }
                else
                {
                    // TCP endpoint for direct access (e.g., local/dev) supporting HTTP/1.1
                    options.ListenLocalhost(5000, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http1;
                    });
                }
            });
    }
}

static void ConfigureMiddleware(WebApplication app, bool isMockServer)
{
    if (isMockServer)
    {
        return;
    }

    app.UseHostFiltering();
    app.UseTenantResolution();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions { AllowStatusCode404Response = true });
    }

    app.UseSecurityHeaders();
}

static void ConfigureEndpoints(WebApplication app)
{
    app.UseStatusCodePages();
    app.MapHealthChecks("/api/health");
    app.MapApiRoutes();
}

static async Task RunApplicationAsync(WebApplication app, Stopwatch timer)
{
    ProgramLoggerMessages log = new(app.Services.GetRequiredService<ILogger<Program>>());
    log.AppLaunch(timer.Elapsed);

    try
    {
        await app.RunAsync();
        log.AppShutdown(timer.Elapsed);
    }
    catch (Exception exception)
    {
        log.AppCrash(exception, timer.Elapsed);
        throw;
    }
    finally
    {
        NLog.LogManager.Shutdown();
    }
}

static void ConfigureBearerAuthentication(OpenApiOptions options)
{
    OpenApiSecuritySchemeReference schemaReference = new(JwtBearerDefaults.AuthenticationScheme);

    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Servers = [new() { Url = "/" }];
        document.Info.Description = "Booqr API";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes.Add(
            JwtBearerDefaults.AuthenticationScheme,
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Name = JwtBearerDefaults.AuthenticationScheme,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT"
            });
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement { [schemaReference] = [] });
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>()
            .Any())
        {
            operation.Security = [new() { { schemaReference, [] } }];
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized" });

            if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>()
                .Any(x => x.Roles?.Length != 0))
            {
                operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
            }
        }

        return Task.CompletedTask;
    });
}
