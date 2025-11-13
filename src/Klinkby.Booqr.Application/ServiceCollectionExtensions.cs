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
        services.AddOptions<ReminderMailSettings>()
            .Bind(configuration.GetSection("ReminderMail"))
            .ValidateOnStart();
        services
            .AddOptions<JwtSettings>()
            .Bind(configuration.GetSection("Jwt"))
            .ValidateOnStart();

        if (!inhibitServices)
        {
            BoundedChannelOptions options = new(100)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = true,
            };

            services.AddBoundedChannel<Message>(options);
            services.AddHostedService<EmailBackgroundService>();

            services.AddBoundedChannel<Activity>(options);
            services.AddHostedService<ActivityBackgroundService>();

            services.AddScoped<IActivityRecorder, ActivityRecorder>();

            services.AddHostedService<ReminderMailService>();
        }

        return services;
    }

    private static void AddBoundedChannel<T>(this IServiceCollection services, BoundedChannelOptions options)
    {
        var channel = Channel.CreateBounded<T>(options);
        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);
    }

    [GenerateServiceRegistrations(
        AssignableTo = typeof(ICommand<>), AsSelf = true)]
    [GenerateServiceRegistrations(
        AssignableTo = typeof(ICommand<,>), AsSelf = true)]
    private static partial IServiceCollection AddCommands(this IServiceCollection services);
}
