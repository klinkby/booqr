using System.Threading.Channels;
using Klinkby.Booqr.Application;
using Klinkby.Booqr.Application.Services;
using Klinkby.Booqr.Application.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ServiceScan.SourceGenerator;
using EmailBackgroundService = Klinkby.Booqr.Application.Services.EmailBackgroundService;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring application services in the dependency injection container.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds application-layer services to the dependency injection container, including commands, background services, and configuration.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration containing settings for JWT and reminder emails.</param>
    /// <param name="inhibitServices">If <c>true</c>, background services (email, activity, reminders) will not be registered.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services,
        IConfiguration configuration, bool inhibitServices)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddCommands();

        if (inhibitServices)
        {
            return services;
        }

        services
            .AddOptions<ReminderMailSettings>()
            .Bind(configuration.GetSection("ReminderMail"))
            .ValidateOnStart();

        services
            .AddSingleton<IValidateOptions<JwtSettings>, ValidateJwtSettings>()
            .AddOptions<JwtSettings>()
            .Bind(configuration.GetRequiredSection("Jwt"))
            .ValidateOnStart();

        services
            .AddSingleton<IValidateOptions<PasswordSettings>, ValidatePasswordSettings>()
            .AddOptions<PasswordSettings>()
            .Bind(configuration.GetRequiredSection("Password"))
            .ValidateOnStart();

        BoundedChannelOptions options = new(100)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = true,
        };

        // emails
        services.AddBoundedChannel<Message>(options);
        services.AddHostedService<EmailBackgroundService>();

        // activities
        services.AddScoped<IActivityRecorder, ActivityRecorder>();

        // auth
        services.AddTransient<IOAuth, OAuth>();

        // expiring queries
        services.AddSingleton<IExpiringQueryString, ExpiringQueryString>();

        return services;
    }

    private static void AddBoundedChannel<T>(this IServiceCollection services, BoundedChannelOptions options)
    {
        var channel = Channel.CreateBounded<T>(options);
        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);
    }

    /// <summary>
    /// Adds the cross-tenant scheduled background workers that run in the worker process.
    /// These services (ReminderMailService, FlushTokenService) span all tenants and run via the booqr_batch (BYPASSRLS) connection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddScheduledWorkers(this IServiceCollection services)
    {
        services.AddHostedService<ReminderMailService>();
        services.AddHostedService<FlushTokenService>();
        return services;
    }

    [GenerateServiceRegistrations(
        AssignableTo = typeof(ICommand<,>), AsSelf = true)]
    private static partial void AddCommands(this IServiceCollection services);
}
