using System.Diagnostics;
using System.Reflection;
using Klinkby.Booqr.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NLog.Web;

var timer = Stopwatch.StartNew();

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

// https://learn.microsoft.com/da-dk/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-9.0&tabs=visual-studio%2Cvisual-studio-code#customizing-run-time-behavior-during-build-time-document-generation
var isMockServer = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

if (!isMockServer)
{
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();
}

ConfigurationManager configuration = builder.Configuration;
builder
    .Services
    .AddSingleton<TimeProvider>(static _ => TimeProvider.System)
    .AddApplication(configuration.GetSection("Application"))
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

WebApplication app = builder.Build();
if (!isMockServer)
{
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

app.UseStatusCodePages();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = static ctx =>
    {
        ctx.Context.Response.Headers.Append(
            "Cache-Control", $"public, max-age=86400");
    }
});
app.MapApiRoutes();

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
return;

static void ConfigureBearerAuthentication(OpenApiOptions options)
{
    var schemaReference = new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme);

    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Servers = [new() { Url = "/" }];
        document.Info.Description = "Booqr API";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes.Add(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Name = JwtBearerDefaults.AuthenticationScheme, Scheme = JwtBearerDefaults.AuthenticationScheme, BearerFormat = "JWT" });
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
