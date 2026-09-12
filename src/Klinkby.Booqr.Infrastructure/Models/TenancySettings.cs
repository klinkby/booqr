using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Infrastructure.Models;

/// <summary>
///     Provides configuration settings for multi-tenancy host resolution and policy.
/// </summary>
/// <remarks>
///     This record contains the core tenancy configuration that controls how the system resolves
///     tenant identity from HTTP request hosts and manages domain-level routing policies.
///     Bound from the <c>Tenancy</c> configuration section by
///     <see cref="Microsoft.Extensions.DependencyInjection.ServiceCollectionExtensions.AddInfrastructure" />.
/// </remarks>
public sealed record TenancySettings
{
    /// <summary>
    ///     Gets or initializes the base domain (e.g., "booqr.dk") used for tenant host resolution.
    /// </summary>
    /// <remarks>
    ///     Tenant subdomains are matched against this base domain. For example, with base domain
    ///     "booqr.dk", the host "acme.booqr.dk" resolves the tenant with slug "acme". The apex
    ///     (naked base domain) and reserved subdomains (e.g. "www", "status", "mta-sts") resolve to no tenant.
    /// </remarks>
    /// <value>The base domain, typically a DNS apex (e.g., "booqr.dk").</value>
    [Required]
    public required string BaseDomain { get; set; }

    /// <summary>
    ///     Gets or initializes the collection of reserved subdomains that are not treated as tenant slugs.
    /// </summary>
    /// <remarks>
    ///     Hosts matching these subdomains (e.g., "www.booqr.dk", "api.booqr.dk") do not resolve a tenant
    ///     and are typically used for marketing sites or infrastructure routes. This prevents slugs from
    ///     being claimed by marketing/ops and ensures unambiguous tenant routing.
    /// </remarks>
    /// <value>An array of lowercase reserved subdomain names (e.g., ["www", "api"]), or empty if none are reserved.</value>
    [Required]
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays",
        Justification = "Options-pattern binding target; bound once from configuration, not a mutation-prone public API surface.")]
    public required string[] ReservedSubdomains { get; set; }
}

[OptionsValidator]
internal sealed partial class ValidateTenancySettings : IValidateOptions<TenancySettings>;
