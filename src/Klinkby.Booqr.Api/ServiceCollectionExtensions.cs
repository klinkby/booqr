using System.Text;
using Klinkby.Booqr.Application.Models;
using Microsoft.IdentityModel.Tokens;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services, Action<JwtSettings> configureJwt)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                JwtSettings jwt = new();
                configureJwt(jwt);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwt.Key!)),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
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
