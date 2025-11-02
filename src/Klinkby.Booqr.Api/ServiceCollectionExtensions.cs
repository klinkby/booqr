using System.Text;
using Klinkby.Booqr.Application.Models;
using Microsoft.IdentityModel.Tokens;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(configuration.GetValue<string>(nameof(JwtSettings.Key))!)),
                    ValidateIssuer = true,
                    ValidIssuer = configuration.GetValue<string>(nameof(JwtSettings.Issuer)),
                    ValidateAudience = true,
                    ValidAudience = configuration.GetValue<string>(nameof(JwtSettings.Audience)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(UserRole.Admin, policy => policy.RequireRole(UserRole.Admin))
            .AddPolicy(UserRole.Employee, policy => policy.RequireRole(UserRole.Admin, UserRole.Employee))
            .AddPolicy(UserRole.Customer,
                policy => policy.RequireRole(UserRole.Admin, UserRole.Employee, UserRole.Customer));

        services.AddProblemDetails();
        services.AddHealthChecks();
        services.AddValidation();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });
        services.AddScoped<IETagProvider, ETagProvider>();
        services.AddSingleton<ETagProviderEndPointFilter>();
        return services;
    }
}
