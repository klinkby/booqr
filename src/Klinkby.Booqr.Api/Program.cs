using System.Reflection;
using Klinkby.Booqr.Api;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
// https://learn.microsoft.com/da-dk/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-9.0&tabs=visual-studio%2Cvisual-studio-code#customizing-run-time-behavior-during-build-time-document-generation
var isMockServer = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
ConfigurationManager configuration = builder.Configuration;
builder
    .Services
    .AddSingleton<TimeProvider>(_ => TimeProvider.System)
    .AddApplication(options => configuration.GetSection("Application").Bind(options))
    .AddInfrastructure(options => configuration.GetSection("Infrastructure").Bind(options))
    .AddApi(options => configuration.GetSection("Application:Jwt").Bind(options),
        options => configuration.GetSection("W3CLogging").Bind(options));

if (isMockServer)
{
    builder.Services.AddOpenApi();
}
else
{
    builder.WebHost.UseKestrelHttpsConfiguration();
    builder.Services.AddHsts(options => options
        .MaxAge = TimeSpan.FromSeconds(63072000)); // https://developer.mozilla.org/en-US/observatory/docs/faq#can_i_scan_non-websites_such_as_api_endpoints
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
        app.UseHsts();
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
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append(
            "Cache-Control", $"public, max-age={TimeSpan.FromDays(1).TotalSeconds}");
    }
});
Routes.MapApi(app);

await app.RunAsync();
