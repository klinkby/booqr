using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Threading.Channels;
using Klinkby.Booqr.Infrastructure;
using Klinkby.Booqr.Infrastructure.MailClient;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using ServiceScan.SourceGenerator;
using EmailLabsMailClient = Klinkby.Booqr.Infrastructure.MailClient.EmailLabsMailClient;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,
        Action<InfrastructureSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        InfrastructureSettings settings = new();
        configure(settings);
        services.AddSingleton<IOptions<InfrastructureSettings>>(_ => Options.Options.Create(settings));

        services.ConfigureEmailLabsHttpClient(settings.MailClientApiKey ?? string.Empty);
        services.ConfigureEmailChannel();
        services.AddSingleton<IMailClient, EmailLabsMailClient>();

        return services
            .AddNpgsqlSlimDataSource(settings.ConnectionString ?? "", serviceKey: nameof(ConnectionProvider))
            .AddScoped<ITransaction, Transaction>()
            .AddScoped<IConnectionProvider, ConnectionProvider>()
            .AddRepositories();
    }

    private static void ConfigureEmailLabsHttpClient(this IServiceCollection services, string apiKey)
    {
        // https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience?tabs=dotnet-cli
        services
            .AddHttpClient(
                nameof(EmailLabsMailClient),
                client =>
                {
                    client.BaseAddress = new Uri("https://api.emaillabs.net.pl/");
                    var codedValue = Convert.ToBase64String(Encoding.ASCII.GetBytes(apiKey));
                    HttpRequestHeaders headers = client.DefaultRequestHeaders;
                    headers.Authorization = new AuthenticationHeaderValue("Basic", codedValue);
                    headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(MediaTypeNames.Application.Json));
                    headers.UserAgent.Add(new ProductInfoHeaderValue("Booqr", "1.0"));
                })
            .AddAsKeyed(ServiceLifetime.Singleton)
            .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
    }

    private static void ConfigureEmailChannel(this IServiceCollection services, int capacity = 100)
    {
        var channel = Channel.CreateBounded<Message>(capacity);
        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);
        services.AddHostedService<EmailBackgroundService>();
    }

    [GenerateServiceRegistrations(
        AssignableTo = typeof(IRepository),
        AsImplementedInterfaces = true)]
    private static partial IServiceCollection AddRepositories(this IServiceCollection services);
}
