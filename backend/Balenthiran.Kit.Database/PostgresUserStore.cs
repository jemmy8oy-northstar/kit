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
        )
        """;

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
    public void Dispose() => source.Dispose();

    private NpgsqlCommand Command(string sql, params string[] values)
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
