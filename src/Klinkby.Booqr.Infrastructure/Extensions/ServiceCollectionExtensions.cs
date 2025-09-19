using Klinkby.Booqr.Infrastructure;
using ServiceScan.SourceGenerator;

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
        return services
            .AddNpgsqlSlimDataSource(settings.ConnectionString ?? "", serviceKey: nameof(ConnectionProvider))
            .AddScoped<ITransaction, Transaction>()
            .AddScoped<IConnectionProvider, ConnectionProvider>()
            .AddRepositories();
    }

    [GenerateServiceRegistrations(
        AssignableTo = typeof(IRepository),
        AsImplementedInterfaces = true)]
    private static partial IServiceCollection AddRepositories(this IServiceCollection services);
}
