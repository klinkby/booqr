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
}

app.UseStatusCodePages();
app.UseStaticFiles();
Routes.MapApi(app);

await app.RunAsync();
