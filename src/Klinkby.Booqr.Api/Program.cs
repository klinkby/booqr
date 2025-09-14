using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NLog.Web;

namespace Klinkby.Booqr.Api;

internal partial class Program
{
    public async static Task Main(string[] args)
    {
        Stopwatch timer = Stopwatch.StartNew();

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
            .AddApplication(options => configuration.GetSection("Application").Bind(options))
            .AddInfrastructure(options => configuration.GetSection("Infrastructure").Bind(options))
            .AddApi(options => configuration.GetSection("Application:Jwt").Bind(options),
                options => configuration.GetSection("W3CLogging").Bind(options));

        if (isMockServer)
        {
            builder.Services.AddOpenApi(ConfigureBearerAuthentication);
        }
        else
        {
            builder.WebHost.UseKestrelHttpsConfiguration();
        }

        WebApplication app = builder.Build();
        if (!isMockServer)
        {
            app.UseAuthorization();
            app.UseHealthChecks("/api/health");
            app.UseW3CLogging();
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(new ExceptionHandlerOptions
                {
                    AllowStatusCode404Response = true,
                    StatusCodeSelector = StatusCodeSelector.Map
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
                    "Cache-Control", $"public, max-age={TimeSpan.FromDays(1).TotalSeconds}");
            }
        });
        Routes.MapApi(app);

        LoggerMessages log = new(app.Services.GetRequiredService<ILogger<Program>>());
        log.LogAppLaunch(timer.Elapsed);
        try
        {
            await app.RunAsync();
            log.LogAppShutdown(timer.Elapsed);
        }
        catch (Exception exception)
        {
            log.LogAppCrash(exception, timer.Elapsed);
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
    }

    private sealed partial class LoggerMessages(ILogger logger)
    {
        [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Ref by SG")]
        private readonly ILogger _logger = logger;

        [LoggerMessage(1, LogLevel.Information, "App initialized in {TimeSpan}")]
        public partial void LogAppLaunch(TimeSpan timeSpan);

        [LoggerMessage(2, LogLevel.Information, "App shutdown ran for {TimeSpan}")]
        public partial void LogAppShutdown(TimeSpan timeSpan);

        [LoggerMessage(3, LogLevel.Error, "App crash after {TimeSpan}")]
        public partial void LogAppCrash(Exception exception, TimeSpan timeSpan);
    }
}
