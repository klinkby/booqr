using System.Text;
using Klinkby.Booqr.Api;
using Klinkby.Booqr.Application.Models;
using Klinkby.Booqr.Infrastructure.Services;
using Microsoft.IdentityModel.Tokens;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

internal static class ServiceCollectionExtensions
{
    internal static void AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        ConfigureAuthentication(services, configuration);
        ConfigureAuthorization(services);
        ConfigureProblemDetails(services);
        ConfigureHealthChecks(services);
        ConfigureJson(services);
        ConfigureRequestMetadata(services);
    }

    private static void ConfigureAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration.GetValue<string>(nameof(JwtSettings.Key))!)),
                    ValidateIssuer = true,
                    ValidIssuer = configuration.GetValue<string>(nameof(JwtSettings.Issuer)),
                    ValidateAudience = true,
                    ValidAudience = configuration.GetValue<string>(nameof(JwtSettings.Audience)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });
    }

    private static void ConfigureAuthorization(IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(UserRole.Admin, policy => policy.RequireRole(UserRole.Admin))
            .AddPolicy(UserRole.Employee, policy => policy.RequireRole(UserRole.Admin, UserRole.Employee))
            .AddPolicy(UserRole.Customer,
                policy => policy.RequireRole(UserRole.Admin, UserRole.Employee, UserRole.Customer));
    }

    private static void ConfigureProblemDetails(IServiceCollection services)
    {
        services.AddProblemDetails(static options =>
            options.CustomizeProblemDetails = static context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
    }

    private static void ConfigureHealthChecks(IServiceCollection services)
    {
        services
            .AddScoped<DatabaseHealthCheck>()
            .AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(nameof(DatabaseHealthCheck));
    }

    private static void ConfigureJson(IServiceCollection services)
    {
        services.AddValidation();
        services.ConfigureHttpJsonOptions(static options =>
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));
    }

    private static void ConfigureRequestMetadata(IServiceCollection services)
    {
        services.AddScoped<IRequestMetadata, RequestMetadata>();
        services.AddSingleton<RequestMetadataEndPointFilter>();
    }
}
