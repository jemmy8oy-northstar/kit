using Balenthiran.Kit.Abstractions.Services;
using Npgsql;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="IUserMemoryStore"/> in the database Kit runs (<c>KIT_DATABASE</c>), beside the accounts
/// (BEH-USERS-4): one text body per user. The table is made on first use, as
/// <see cref="PostgresUserStore"/> makes its own, so a database that is down costs a read, not Kit.
/// </summary>
public sealed class PostgresUserMemoryStore(string connectionString) : IUserMemoryStore, IDisposable
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS user_memory (
            user_id text PRIMARY KEY,
            body text NOT NULL,
            updated timestamptz NOT NULL DEFAULT now()
        )
        """;

    private readonly NpgsqlDataSource source = NpgsqlDataSource.Create(connectionString);
    private readonly Lock gate = new();
    private bool made;

    /// <inheritdoc />
    public string? Read(string userId)
    {
        using var cmd = Command("SELECT body FROM user_memory WHERE user_id = $1", userId);
        return cmd.ExecuteScalar() as string;
    }

    /// <inheritdoc />
    public void Write(string userId, string body)
    {
        using var cmd = Command(
            "INSERT INTO user_memory (user_id, body) VALUES ($1, $2) ON CONFLICT (user_id) DO UPDATE SET body = EXCLUDED.body, updated = now()",
            userId,
            body);
        cmd.ExecuteNonQuery();
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
