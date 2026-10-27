using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Npgsql;

namespace Balenthiran.Kit.Database;

/// <summary>
/// Early-access accounts (BEH-USERS-1) in the database Kit runs (<c>KIT_DATABASE</c>), the one
/// BEH-USERS-4 keeps a user's memory in. One table, made on first use; the corpora stay in git.
/// </summary>
public sealed class PostgresUserStore(string connectionString) : IUserStore, IDisposable
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS kit_users (
            name text PRIMARY KEY,
            hash text NOT NULL,
            created timestamptz NOT NULL DEFAULT now(),
            changed timestamptz
        );
        ALTER TABLE kit_users ADD COLUMN IF NOT EXISTS github_installation bigint UNIQUE;
        ALTER TABLE kit_users ADD COLUMN IF NOT EXISTS github_account text
        """;

    // Postgres's code for a broken UNIQUE: here, an installation another user already holds.
    private const string UniqueViolation = "23505";

    private readonly NpgsqlDataSource source = NpgsqlDataSource.Create(connectionString);

    // The table is made on first use rather than at startup, so a database that is down costs a
    // sign-in, not the whole Kit; and a failed attempt is retried by the next call.
    private readonly Lock gate = new();
    private bool made;

    /// <inheritdoc />
    public string? HashOf(string name)
    {
        using var cmd = Command("SELECT hash FROM kit_users WHERE name = $1", name);
        return cmd.ExecuteScalar() as string;
    }

    /// <inheritdoc />
    public bool Add(string name, string hash)
    {
        using var cmd = Command("INSERT INTO kit_users (name, hash) VALUES ($1, $2) ON CONFLICT (name) DO NOTHING", name, hash);
        return cmd.ExecuteNonQuery() == 1;
    }

    /// <inheritdoc />
    public bool SetHash(string name, string hash)
    {
        using var cmd = Command("UPDATE kit_users SET hash = $2, changed = now() WHERE name = $1", name, hash);
        return cmd.ExecuteNonQuery() == 1;
    }

    /// <inheritdoc />
    public IGitHubLink? GitHubOf(string name)
    {
        using var cmd = Command("SELECT github_installation, github_account FROM kit_users WHERE name = $1 AND github_installation IS NOT NULL", name);
        using var row = cmd.ExecuteReader();
        return row.Read() ? new GitHubInstallation(row.GetInt64(0), row.GetString(1)) : null;
    }

    /// <inheritdoc />
    public bool SetGitHub(string name, IGitHubLink link)
    {
        using var cmd = Command("UPDATE kit_users SET github_installation = $2, github_account = $3, changed = now() WHERE name = $1", name, link.Installation, link.Account);
        try
        {
            return cmd.ExecuteNonQuery() == 1;
        }
        catch (PostgresException e) when (e.SqlState == UniqueViolation)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public string? UserOfInstallation(long installation)
    {
        using var cmd = Command("SELECT name FROM kit_users WHERE github_installation = $1", installation);
        return cmd.ExecuteScalar() as string;
    }

    /// <inheritdoc />
    public void Dispose() => source.Dispose();

    private NpgsqlCommand Command(string sql, params object[] values)
    {
        EnsureSchema();
        var cmd = source.CreateCommand(sql);
        foreach (var v in values)
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = v });
        }

        return cmd;
    }

    private void EnsureSchema()
    {
        lock (gate)
        {
            if (made)
            {
                return;
            }

            using var cmd = source.CreateCommand(Schema);
            cmd.ExecuteNonQuery();
            made = true;
        }
    }
}
