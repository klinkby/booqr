using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Klinkby.Booqr.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http.Resilience;
using ServiceScan.SourceGenerator;
using EmailLabsMailClient = Klinkby.Booqr.Infrastructure.Services.EmailLabsMailClient;
using Transaction = Klinkby.Booqr.Infrastructure.Services.Transaction;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<InfrastructureSettings>()
            .Bind(configuration)
            .ValidateOnStart();
        services.ConfigureEmailLabsHttpClient(
            configuration.GetValue<Uri>(nameof(InfrastructureSettings.MailClientBaseAddress)),
            configuration.GetValue<string>(nameof(InfrastructureSettings.MailClientApiKey)));
        services.AddNpgsqlSlimDataSource(
            configuration.GetValue<string>(nameof(InfrastructureSettings.ConnectionString)) ?? string.Empty,
            serviceKey: nameof(ConnectionProvider));
        services.AddSingleton<IMailClient, EmailLabsMailClient>();
        services.AddScoped<ITransaction, Transaction>();
        services.AddScoped<IConnectionProvider, ConnectionProvider>();
        services.AddRepositories();
        return services;
    }

    private static void ConfigureEmailLabsHttpClient(this IServiceCollection services, Uri? baseAddress, string? apiKey)
    {
        // https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience?tabs=dotnet-cli
        services
            .AddHttpClient(nameof(EmailLabsMailClient), client =>
                ConfigureHttpClient(client, baseAddress, apiKey))
            .AddAsKeyed(ServiceLifetime.Singleton)
            .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
    }

    private static void ConfigureHttpClient(HttpClient client, Uri? baseAddress, string? apiKey)
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
}
