using Klinkby.Booqr.Application;
using ServiceScan.SourceGenerator;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services,
        Action<ApplicationSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        ApplicationSettings settings = new();
        configure(settings);
        services
            .AddCommands()
            .Configure(configure)
            .Configure<JwtSettings>(options =>
            {
                options.Key = settings.Jwt.Key;
                options.Issuer = settings.Jwt.Issuer;
                options.Audience = settings.Jwt.Audience;
            });
        return services;
    }

    [GenerateServiceRegistrations(
        AssignableTo = typeof(ICommand<>), AsSelf = true)]
    [GenerateServiceRegistrations(
        AssignableTo = typeof(ICommand<,>), AsSelf = true)]
    private static partial IServiceCollection AddCommands(this IServiceCollection services);
}
