using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Npgsql;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="IPushStore"/> in the database Kit runs (<c>KIT_DATABASE</c>), beside the accounts: a
/// phone's push subscription (BEH-NOTIFY-3) and the mark of the feed entry last pushed, so a
/// restart neither forgets his phone nor pushes the feed again. Tables are made on first use, as
/// <see cref="PostgresUserStore"/> makes its own, so a database that is down costs a push, not Kit.
/// </summary>
public sealed class PostgresPushStore(string connectionString) : IPushStore, IDisposable
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS push_subscriptions (
            endpoint text PRIMARY KEY,
            p256dh text NOT NULL,
            auth text NOT NULL,
            created timestamptz NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS push_state (
            name text PRIMARY KEY,
            value text NOT NULL
        )
        """;

    private const string MarkName = "mark";

    private readonly NpgsqlDataSource source = NpgsqlDataSource.Create(connectionString);
    private readonly Lock gate = new();
    private bool made;

    /// <inheritdoc />
    public bool Add(IPushSubscription subscription)
    {
        // xmax = 0 only on a row this statement inserted: the way to tell a new endpoint from a re-subscribe.
        using var cmd = Command(
            "INSERT INTO push_subscriptions (endpoint, p256dh, auth) VALUES ($1, $2, $3) ON CONFLICT (endpoint) DO UPDATE SET p256dh = EXCLUDED.p256dh, auth = EXCLUDED.auth RETURNING (xmax = 0)",
            subscription.Endpoint,
            subscription.P256dh,
            subscription.Auth);
        return cmd.ExecuteScalar() is true;
    }

    /// <inheritdoc />
    public bool Remove(string endpoint)
    {
        using var cmd = Command("DELETE FROM push_subscriptions WHERE endpoint = $1", endpoint);
        return cmd.ExecuteNonQuery() == 1;
    }

    /// <inheritdoc />
    public IReadOnlyList<IPushSubscription> All()
    {
        using var cmd = Command("SELECT endpoint, p256dh, auth FROM push_subscriptions ORDER BY created");
        using var rows = cmd.ExecuteReader();
        var all = new List<IPushSubscription>();
        while (rows.Read())
        {
            all.Add(new StoredPushSubscription { Endpoint = rows.GetString(0), P256dh = rows.GetString(1), Auth = rows.GetString(2) });
        }

        return all;
    }

    /// <inheritdoc />
    public string? Mark()
    {
        using var cmd = Command("SELECT value FROM push_state WHERE name = $1", MarkName);
        return cmd.ExecuteScalar() as string;
    }

    /// <inheritdoc />
    public bool AdvanceMark(string? expected, string next)
    {
        // One statement either way, so two ticks (or two pods) cannot both move the same mark.
        using var cmd = expected is null
            ? Command("INSERT INTO push_state (name, value) VALUES ($1, $2) ON CONFLICT (name) DO NOTHING", MarkName, next)
            : Command("UPDATE push_state SET value = $2 WHERE name = $1 AND value = $3", MarkName, next, expected);
        return cmd.ExecuteNonQuery() == 1;
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
