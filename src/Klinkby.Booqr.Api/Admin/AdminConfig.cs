using Npgsql;

namespace Klinkby.Booqr.Api.Admin;

/// <summary>
///     Environment-variable configuration contract for the admin CLI (<see cref="AdminRunner" />).
///     Deliberately reads plain environment variables (not <c>IOptions</c>/appsettings binding) —
///     the admin container is a short-lived CLI process, not the web host, and this keeps Control
///     free of any ASP.NET/hosting dependency.
/// </summary>
/// <remarks>
///     <para>
///         <b>Env var contract</b> (mirrors the roles/credentials in
///         <c>redist/initdb/01-control-plane.sql</c> and the <c>Infrastructure:*</c> appsettings
///         shape used by the API):
///     </para>
///     <list type="bullet">
///         <item><description><c>BOOQR_ADMIN_CONNECTION_STRING</c> — base PostgreSQL connection string (host/socket + database only, no credentials).</description></item>
///         <item><description><c>BOOQR_MIGRATOR_USERNAME</c> / <c>BOOQR_MIGRATOR_PASSWORD</c> — <c>booqr_migrator</c> credentials. Used by <c>--migrate</c>, <c>--provision</c>, <c>--deprovision</c>, <c>--rotate</c>.</description></item>
///         <item><description><c>BOOQR_BATCH_USERNAME</c> / <c>BOOQR_BATCH_PASSWORD</c> — <c>booqr_batch</c> credentials. Not required by the current four commands (reserved for future cross-tenant admin tooling); validated lazily, not at startup.</description></item>
///         <item><description><c>BOOQR_TENANT_MASTER_SECRET</c> — the current HMAC master secret used to derive <c>t_&lt;id&gt;</c> passwords. Used by <c>--provision</c> (to set the new role's password and to open a tenant connection for the seed admin insert).</description></item>
///         <item><description><c>BOOQR_TENANT_MASTER_SECRET_NEW</c> — the new master secret <c>--rotate</c> re-derives every tenant password under. Kept distinct from <c>BOOQR_TENANT_MASTER_SECRET</c> so rotation is explicit and can't silently no-op by rotating a secret onto itself.</description></item>
///     </list>
///     <para>Never logs any secret value read from these variables.</para>
/// </remarks>
internal static class AdminConfig
{
    private const string ConnectionStringVar = "BOOQR_ADMIN_CONNECTION_STRING";
    private const string MigratorUsernameVar = "BOOQR_MIGRATOR_USERNAME";
    private const string MigratorPasswordVar = "BOOQR_MIGRATOR_PASSWORD";
    private const string MasterSecretVar = "BOOQR_TENANT_MASTER_SECRET";
    private const string NewMasterSecretVar = "BOOQR_TENANT_MASTER_SECRET_NEW";

    /// <summary>Reads a required environment variable or throws with a clear, secret-free message.</summary>
    internal static string Require(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required environment variable '{variableName}' is not set.");
        }

        return value;
    }

    /// <summary>Builds a connection string authenticating as <c>booqr_migrator</c>.</summary>
    internal static string MigratorConnectionString()
    {
        NpgsqlConnectionStringBuilder builder = new(Require(ConnectionStringVar))
        {
            Username = Require(MigratorUsernameVar),
            Password = Require(MigratorPasswordVar)
        };
        return builder.ConnectionString;
    }

    /// <summary>The base connection string (host/socket + database, no credentials).</summary>
    internal static string BaseConnectionString() => Require(ConnectionStringVar);

    /// <summary>The current master secret used to derive <c>t_&lt;id&gt;</c> passwords.</summary>
    internal static string MasterSecret() => Require(MasterSecretVar);

    /// <summary>The new master secret <c>--rotate</c> re-derives every tenant password under.</summary>
    internal static string NewMasterSecret() => Require(NewMasterSecretVar);
}
