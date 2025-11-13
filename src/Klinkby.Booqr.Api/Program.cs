using System.Diagnostics;
using System.Reflection;
using Klinkby.Booqr.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NLog.Web;

const string StaticCacheControlValue = "public, max-age=86400";

Stopwatch timer = Stopwatch.StartNew();

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

// Detect if running in OpenAPI document generation mode
// https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi
bool isMockServer = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

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
        .AddSingleton<TimeProvider>(static _ => TimeProvider.System)
        .AddApplication(configuration.GetSection("Application"), isMockServer)
        .AddInfrastructure(configuration.GetSection("Infrastructure"))
        .AddApi(configuration.GetSection("Application:Jwt"));

    if (isMockServer)
    {
        builder.Services.AddOpenApi(ConfigureBearerAuthentication);
    }
    else
    {
        builder.WebHost.UseKestrelCore();
    }
}

static void ConfigureMiddleware(WebApplication app, bool isMockServer)
{
    if (isMockServer)
    {
        return;
    }

    app.UseAuthorization();
    app.UseHealthChecks("/health");

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
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = static ctx =>
        {
            ctx.Context.Response.Headers.Append("Cache-Control", StaticCacheControlValue);
        }
    });
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
