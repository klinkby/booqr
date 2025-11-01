using System.Threading.Channels;
using Klinkby.Booqr.Application.Services;
using Microsoft.Extensions.Configuration;
using ServiceScan.SourceGenerator;
using EmailBackgroundService = Klinkby.Booqr.Application.Services.EmailBackgroundService;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddCommands()
            .Configure<ReminderMailSettings>(configuration.GetSection("ReminderMail"))
            .Configure<JwtSettings>(configuration.GetSection("Jwt"))
            .ConfigureEmailChannel();

        return services;
    }

    private static void ConfigureEmailChannel(this IServiceCollection services, int capacity = 100)
    {
        var channel = Channel.CreateBounded<Message>(capacity);
        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);
        services.AddHostedService<EmailBackgroundService>();
        services.AddHostedService<ReminderMailService>();
    }

    [GenerateServiceRegistrations(
        AssignableTo = typeof(ICommand<>), AsSelf = true)]
    [GenerateServiceRegistrations(
        AssignableTo = typeof(ICommand<,>), AsSelf = true)]
    private static partial IServiceCollection AddCommands(this IServiceCollection services);
}
