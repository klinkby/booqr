using System.Threading.Channels;
using Klinkby.Booqr.Application;
using Klinkby.Booqr.Application.Services;
using Microsoft.Extensions.Configuration;
using ServiceScan.SourceGenerator;
using EmailBackgroundService = Klinkby.Booqr.Application.Services.EmailBackgroundService;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services,
        IConfiguration configuration, bool inhibitServices)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddCommands();
        services.ConfigureEmailChannel();
        services.AddOptions<ReminderMailSettings>()
            .Bind(configuration.GetSection("ReminderMail"))
            .ValidateOnStart();
        services
            .AddOptions<JwtSettings>()
            .Bind(configuration.GetSection("Jwt"))
            .ValidateOnStart();

        if (!inhibitServices)
        {
            services.AddHostedService<EmailBackgroundService>();
            services.AddHostedService<ReminderMailService>();
        }

        return services;
    }

    private static void ConfigureEmailChannel(this IServiceCollection services, int capacity = 100)
    {
        var channel = Channel.CreateBounded<Message>(capacity);
        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);
    }

    [GenerateServiceRegistrations(
        AssignableTo = typeof(ICommand<>), AsSelf = true)]
    [GenerateServiceRegistrations(
        AssignableTo = typeof(ICommand<,>), AsSelf = true)]
    private static partial IServiceCollection AddCommands(this IServiceCollection services);
}
