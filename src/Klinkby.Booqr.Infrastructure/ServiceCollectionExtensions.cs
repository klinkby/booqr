using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Klinkby.Booqr.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceScan.SourceGenerator;
using EmailLabsMailClient = Klinkby.Booqr.Infrastructure.Services.EmailLabsMailClient;
using Transaction = Klinkby.Booqr.Infrastructure.Services.Transaction;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Provides extension methods for configuring infrastructure services in an <see cref="IServiceCollection"/>.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds infrastructure services including database connectivity, repositories, and email services to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The configuration containing infrastructure settings.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is <c>null</c>.</exception>
    /// <remarks>
    ///     This method configures:
    ///     <list type="bullet">
    ///         <item><description>PostgreSQL data source using Npgsql</description></item>
    ///         <item><description>EmailLabs HTTP client with resilience policies</description></item>
    ///         <item><description>Mail client service (<see cref="IMailClient"/>)</description></item>
    ///         <item><description>Unit-of-work management (<see cref="ITransaction"/>)</description></item>
    ///         <item><description>Database connection provider (<see cref="IConnectionProvider"/>)</description></item>
    ///         <item><description>All repository implementations</description></item>
    ///     </list>
    /// </remarks>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddSingleton<IValidateOptions<InfrastructureSettings>, ValidateInfrastructureSettings>()
            .AddOptions<InfrastructureSettings>()
            .Bind(configuration)
            .ValidateOnStart();

        services.ConfigureEmailLabsHttpClient();
        services.AddNpgsqlSlimDataSource(
            "",
            (serviceProvider, builder) =>
            {
                InfrastructureSettings settings =
                    serviceProvider.GetRequiredService<IOptions<InfrastructureSettings>>().Value;
                builder.ConnectionStringBuilder.ConnectionString = settings.ConnectionString;
                PostgreSql(
                    serviceProvider.GetRequiredService<ILogger<InfrastructureSettings>>(),
                    builder.ConnectionStringBuilder.Host);
            }, serviceKey: nameof(ConnectionProvider));
        services.AddSingleton<IMailClient, EmailLabsMailClient>();
        services.AddScoped<ITransaction, Transaction>();
        services.AddScoped<IConnectionProvider, ConnectionProvider>();
        services.AddRepositories();

        return services;
    }

    private static void ConfigureEmailLabsHttpClient(this IServiceCollection services)
    {
        // https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience?tabs=dotnet-cli
        services
            .AddHttpClient(nameof(EmailLabsMailClient),
                static (serviceProvider, client) =>
                {
                    InfrastructureSettings settings = serviceProvider.GetRequiredService<IOptions<InfrastructureSettings>>().Value;
                    EmailLabs(
                        serviceProvider.GetRequiredService<ILogger<InfrastructureSettings>>(),
                        settings.MailClientBaseAddress);
                    ConfigureHttpClient(client, settings.MailClientBaseAddress, settings.MailClientApiKey);
                })
            .AddAsKeyed(ServiceLifetime.Singleton)
            .AddStandardResilienceHandler(static options => options.Retry.DisableForUnsafeHttpMethods());
    }

    private static void ConfigureHttpClient(HttpClient client, Uri baseAddress, string apiKey)
    {
        client.BaseAddress = baseAddress;
        var codedValue = Convert.ToBase64String(Encoding.ASCII.GetBytes(apiKey ?? string.Empty));
        HttpRequestHeaders headers = client.DefaultRequestHeaders;
        headers.Authorization = new AuthenticationHeaderValue("Basic", codedValue);
        headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(MediaTypeNames.Application.Json));
        headers.UserAgent.Add(new ProductInfoHeaderValue("Booqr", "1.0"));
    }

    [GenerateServiceRegistrations(
        AssignableTo = typeof(IRepository),
        AsImplementedInterfaces = true)]
    private static partial IServiceCollection AddRepositories(this IServiceCollection services);

    [LoggerMessage(1040, LogLevel.Information, "PostgreSQL is at {Host}")]
    private static partial void PostgreSql(ILogger logger, string? host);

    [LoggerMessage(1041, LogLevel.Information, "EmailLabs is at {Host}")]
    private static partial void EmailLabs(ILogger logger, Uri host);
}
