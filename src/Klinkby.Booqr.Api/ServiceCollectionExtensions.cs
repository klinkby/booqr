using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Klinkby.Booqr.Api;
using Klinkby.Booqr.Api.Models;
using Klinkby.Booqr.Application;
using Klinkby.Booqr.Application.Models;
using Microsoft.Extensions.Options;
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
        ConfigureRateLimiting(services);
    }

    /// <summary>
    ///     Binds and validates <see cref="TenancySettings" /> (tenant host-resolution config) from the
    ///     given configuration section. Host resolution is an API-layer concern, so the option lives
    ///     in and is registered by the Api project rather than Infrastructure.
    /// </summary>
    internal static IServiceCollection AddTenancy(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddSingleton<IValidateOptions<TenancySettings>, ValidateTenancySettings>()
            .AddOptions<TenancySettings>()
            .Bind(configuration)
            .ValidateOnStart();
        return services;
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
        services.AddExceptionHandler<Klinkby.Booqr.Api.GlobalExceptionHandler>();
    }

    /// <summary>
    ///     Throttles <c>POST /users/change-password</c> per tenant host and user id taken from the link's
    ///     <c>id</c> query parameter. The id is read before the link signature is verified, so it is only
    ///     a partition key: non-numeric values share one bucket, which keeps the number of partitions
    ///     bounded. The coarse per-IP limit is enforced by the HAProxy gateway.
    /// </summary>
    private static void ConfigureRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(static options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = static async (context, cancellation) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                // Same RFC 7807 shape as the HAProxy gateway and the command Problem responses.
                Problem problem = Problem.TooManyRequests;
                HttpResponse response = context.HttpContext.Response;
                response.ContentType = "application/problem+json";
                await response.WriteAsync(
                    $$"""{"type":"{{problem.Type}}","title":"{{problem.Title}}","status":{{problem.HttpStatusCode}},"traceId":"{{context.HttpContext.TraceIdentifier}}"}""",
                    cancellation);
            };
            options.AddPolicy(RateLimitPolicies.ChangePassword, static context =>
            {
                var validId = int.TryParse(context.Request.Query["id"].ToString(), CultureInfo.InvariantCulture, out var id);
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"{context.Request.Host.Host}/{(validId ? id : -1)}",
                    static _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0
                    });
            });
        });
    }

    private static void ConfigureHealthChecks(IServiceCollection services)
    {
        services.AddHealthChecks();
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
