using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Npgsql;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="ISessionCostStore"/> in the database Kit runs (<c>KIT_DATABASE</c>), beside the per-user store
/// (BEH-COST-1): one row per run. The table is made on first use, as <see cref="PostgresUserMemoryStore"/> makes its own.
/// </summary>
public sealed class PostgresSessionCostStore(string connectionString) : ISessionCostStore, IDisposable
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS session_cost (
            run_id text PRIMARY KEY,
            session_date timestamptz NOT NULL,
            project text NOT NULL,
            model text NOT NULL,
            cost_usd numeric NOT NULL
        )
        """;

    private readonly NpgsqlDataSource source = NpgsqlDataSource.Create(connectionString);
    private readonly Lock gate = new();
    private bool made;

    /// <inheritdoc />
    public void Record(ISessionCost cost)
    {
        using var cmd = Command(
            "INSERT INTO session_cost (run_id, session_date, project, model, cost_usd) VALUES ($1, $2, $3, $4, $5) ON CONFLICT (run_id) DO NOTHING",
            cost.RunId,
            cost.Date,
            cost.Project,
            cost.Model,
            cost.CostUsd);
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public IReadOnlyList<ISessionCost> List()
    {
        using var cmd = Command("SELECT run_id, session_date, project, model, cost_usd FROM session_cost ORDER BY session_date");
        using var reader = cmd.ExecuteReader();
        var found = new List<ISessionCost>();
        while (reader.Read())
        {
            found.Add(new Row(
                reader.GetString(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDecimal(4)));
        }

        return found;
    }

    /// <inheritdoc />
    public void Dispose() => source.Dispose();

    /// <summary>One stored row, read back as the cost it was recorded as.</summary>
    private sealed record Row(string RunId, DateTimeOffset Date, string Project, string Model, decimal CostUsd) : ISessionCost;

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
