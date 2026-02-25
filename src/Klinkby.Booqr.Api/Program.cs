using System.Diagnostics;
using System.Reflection;
using Klinkby.Booqr.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NLog.Web;

var timer = Stopwatch.StartNew();

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
return;

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
            .AddInfrastructure(configuration.GetRequiredSection("Infrastructure"));
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

    app.UseAuthorization();
    app.UseHealthChecks("/api/health");

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            AllowStatusCode404Response = true,
            StatusCodeSelector = StatusCode.FromException
        });
    }

    app.UseSecurityHeaders();
}

static void ConfigureEndpoints(WebApplication app)
{
    app.UseStatusCodePages();
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
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            operation.Security = [new() { { schemaReference, [] } }];
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized" });
        }

        return Task.CompletedTask;
    });
}
