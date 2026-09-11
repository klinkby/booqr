using System.Globalization;
using System.Reflection;
using Npgsql;

namespace Klinkby.Booqr.Infrastructure.Services;

/// <summary>
///     Applies pending ordered SQL migration scripts (embedded resources under
///     <c>Migrations/*.sql</c>) to the shared <c>app</c> schema, connected as the elevated
///     <c>booqr_migrator</c> role.
/// </summary>
/// <remarks>
///     Not wired into the normal API startup DI graph — used by the Infrastructure test fixture
///     and (later) the admin CLI, both of which supply the migrator <see cref="NpgsqlDataSource" />
///     explicitly. Each script runs inside one transaction guarded by a Postgres advisory lock,
///     so applying migrations is safe to run concurrently from multiple processes/replicas.
/// </remarks>
public sealed class SchemaMigrator(NpgsqlDataSource migratorDataSource)
{
    /// <summary>
    ///     Arbitrary constant advisory-lock key. Chosen to be specific to this migration engine so
    ///     it doesn't collide with unrelated advisory locks elsewhere in the system.
    /// </summary>
    private const long AdvisoryLockKey = 84_209_001;

    private const string MigrationResourcePrefix = "Klinkby.Booqr.Infrastructure.Migrations.";

    /// <summary>
    ///     Applies every pending migration script (in ascending version order), then runs the
    ///     row-level-security coverage guard.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the coverage guard finds an <c>app</c> table without enabled + forced RLS.
    /// </exception>
    public async Task Migrate(CancellationToken cancellation = default)
    {
        foreach (MigrationScript script in DiscoverMigrations())
        {
            await ApplyIfPending(script, cancellation);
        }

        await AssertRlsCoverage(cancellation);
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Migration SQL is a static embedded resource authored in this repo, never user input.")]
    private async Task ApplyIfPending(MigrationScript script, CancellationToken cancellation)
    {
        await using NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync(cancellation);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellation);

        // Advisory lock scoped to this transaction: released automatically on commit/rollback.
        // Serializes concurrent migrators; combined with the "already applied?" check below this
        // makes re-running Migrate() from multiple processes safe.
        await using (NpgsqlCommand lockCommand = new($"select pg_advisory_xact_lock({AdvisoryLockKey})", connection, transaction))
        {
            await lockCommand.ExecuteNonQueryAsync(cancellation);
        }

        if (await IsAlreadyApplied(script.Version, connection, transaction, cancellation))
        {
            await transaction.RollbackAsync(cancellation);
            return;
        }

        await using (NpgsqlCommand ddlCommand = new(script.Sql, connection, transaction))
        {
            await ddlCommand.ExecuteNonQueryAsync(cancellation);
        }

        await using (NpgsqlCommand recordCommand = new(
                         "insert into public.schema_migrations (version) values ($1)", connection, transaction))
        {
            recordCommand.Parameters.Add(new NpgsqlParameter<int> { TypedValue = script.Version });
            await recordCommand.ExecuteNonQueryAsync(cancellation);
        }

        await transaction.CommitAsync(cancellation);
    }

    private static async Task<bool> IsAlreadyApplied(
        int version, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellation)
    {
        await using NpgsqlCommand command = new(
            "select 1 from public.schema_migrations where version = $1", connection, transaction);
        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = version });
        var result = await command.ExecuteScalarAsync(cancellation);
        return result is not null;
    }

    /// <summary>
    ///     RLS coverage guard: fails the migration if any table (relkind = 'r') in schema
    ///     <c>app</c> lacks both <c>ENABLE</c> and <c>FORCE ROW LEVEL SECURITY</c>. Views are
    ///     excluded — RLS applies through their underlying base tables.
    /// </summary>
    private async Task AssertRlsCoverage(CancellationToken cancellation)
    {
        await using NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync(cancellation);
        await using NpgsqlCommand command = new(
            """
            select c.relname
            from pg_class c
            join pg_namespace n on n.oid = c.relnamespace
            where n.nspname = 'app'
              and c.relkind = 'r'
              and (c.relrowsecurity = false or c.relforcerowsecurity = false)
            order by c.relname
            """, connection);

        List<string> unprotected = [];
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellation))
        {
            while (await reader.ReadAsync(cancellation))
            {
                unprotected.Add(reader.GetString(0));
            }
        }

        if (unprotected.Count > 0)
        {
            throw new InvalidOperationException(
                $"RLS coverage guard failed: table(s) without enabled+forced row level security: {string.Join(", ", unprotected)}");
        }
    }

    /// <summary>
    ///     Discovers embedded migration scripts via <see cref="Assembly.GetManifestResourceNames" />
    ///     (AOT-safe — no reflection-heavy type scanning), ordered by the numeric version parsed
    ///     from the filename (e.g. <c>0001_baseline.sql</c> → 1).
    /// </summary>
    private static List<MigrationScript> DiscoverMigrations()
    {
        Assembly assembly = typeof(SchemaMigrator).Assembly;
        List<MigrationScript> scripts = [];

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(MigrationResourcePrefix, StringComparison.Ordinal)
                || !resourceName.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = resourceName[MigrationResourcePrefix.Length..];
            var version = ParseVersion(fileName);

            using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
            using StreamReader reader = new(stream);
            var sql = reader.ReadToEnd();

            scripts.Add(new MigrationScript(version, fileName, sql));
        }

        scripts.Sort(static (a, b) => a.Version.CompareTo(b.Version));
        return scripts;
    }

    private static int ParseVersion(string fileName)
    {
        var separatorIndex = fileName.IndexOf('_', StringComparison.Ordinal);
        var versionPart = separatorIndex < 0 ? fileName : fileName[..separatorIndex];
        if (!int.TryParse(versionPart, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            throw new InvalidOperationException(
                $"Migration resource '{fileName}' does not start with a numeric version (expected e.g. '0001_baseline.sql').");
        }

        return version;
    }

    private sealed record MigrationScript(int Version, string FileName, string Sql);
}
