using System.Threading.Channels;
using Klinkby.Booqr.Application;
using ServiceScan.SourceGenerator;
using EmailBackgroundService = Klinkby.Booqr.Application.Services.EmailBackgroundService;

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
            })
            .ConfigureEmailChannel();
        return services;
    }

    private static void ConfigureEmailChannel(this IServiceCollection services, int capacity = 100)
    {
        var channel = Channel.CreateBounded<Message>(capacity);
        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);
        services.AddHostedService<EmailBackgroundService>();
    }

    [GenerateServiceRegistrations(
        AssignableTo = typeof(ICommand<>), AsSelf = true)]
    [GenerateServiceRegistrations(
        AssignableTo = typeof(ICommand<,>), AsSelf = true)]
    private static partial IServiceCollection AddCommands(this IServiceCollection services);
}
