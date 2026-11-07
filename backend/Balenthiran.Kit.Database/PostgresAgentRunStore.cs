using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Npgsql;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="IAgentRunStore"/> in the database Kit runs (<c>KIT_DATABASE</c>), beside the accounts
/// (BEH-USERS-9). The table is made on first use, as <see cref="PostgresUserMemoryStore"/> makes its own.
/// Only the SHA-256 of a run token is stored, never the token.
/// </summary>
public sealed class PostgresAgentRunStore(string connectionString) : IAgentRunStore, IDisposable
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS agent_runs (
            id text PRIMARY KEY,
            owner text NOT NULL,
            project text NOT NULL,
            model text NOT NULL,
            status text NOT NULL,
            token_hash text NOT NULL UNIQUE,
            started timestamptz NOT NULL,
            ended timestamptz,
            input_tokens bigint NOT NULL DEFAULT 0,
            output_tokens bigint NOT NULL DEFAULT 0,
            cache_write_tokens bigint NOT NULL DEFAULT 0,
            cache_read_tokens bigint NOT NULL DEFAULT 0,
            cost_usd numeric(14,6) NOT NULL DEFAULT 0,
            failure text
        );
        ALTER TABLE agent_runs ADD COLUMN IF NOT EXISTS failure text;
        ALTER TABLE agent_runs ADD COLUMN IF NOT EXISTS work_item text
        """;

    private const string Columns = "id, owner, project, model, status, started, ended, input_tokens, output_tokens, cache_write_tokens, cache_read_tokens, cost_usd, failure, work_item";

    private readonly NpgsqlDataSource source = NpgsqlDataSource.Create(connectionString);
    private readonly Lock gate = new();
    private bool made;

    /// <inheritdoc />
    public IAgentRun Create(string owner, string project, string model, string tokenHash, DateTimeOffset started, string? workItem = null)
    {
        var id = Guid.NewGuid().ToString("N");
        using var cmd = Command(
            "INSERT INTO agent_runs (id, owner, project, model, status, token_hash, started, work_item) VALUES ($1, $2, $3, $4, $5, $6, $7, $8)",
            id, owner, project, model, AgentRunStatus.Queued, tokenHash, started, workItem is null ? DBNull.Value : workItem);
        cmd.ExecuteNonQuery();
        return new Row { Id = id, Owner = owner, Project = project, Model = model, Status = AgentRunStatus.Queued, Started = started, WorkItem = workItem };
    }

    /// <inheritdoc />
    public IReadOnlyList<IAgentRun> List()
    {
        using var cmd = Command($"SELECT {Columns} FROM agent_runs ORDER BY started DESC, id");
        return Read(cmd);
    }

    /// <inheritdoc />
    public IAgentRun? ByTokenHash(string tokenHash)
    {
        using var cmd = Command($"SELECT {Columns} FROM agent_runs WHERE token_hash = $1", tokenHash);
        return Read(cmd).FirstOrDefault();
    }

    /// <inheritdoc />
    public IAgentRun? AddUsage(string id, AgentUsage usage, decimal costUsd)
    {
        // One UPDATE, so two calls finishing together both count.
        using var cmd = Command(
            $"UPDATE agent_runs SET input_tokens = input_tokens + $2, output_tokens = output_tokens + $3, cache_write_tokens = cache_write_tokens + $4, cache_read_tokens = cache_read_tokens + $5, cost_usd = cost_usd + $6 WHERE id = $1 RETURNING {Columns}",
            id, usage.Input, usage.Output, usage.CacheWrite, usage.CacheRead, costUsd);
        return Read(cmd).FirstOrDefault();
    }

    /// <inheritdoc />
    public void SetStatus(string id, string status, DateTimeOffset? ended = null, string? failure = null)
    {
        using var cmd = Command(
            "UPDATE agent_runs SET status = $2, ended = $3, failure = $4 WHERE id = $1",
            id, status, AgentRunStatus.IsOver(status) && ended is { } e ? e : DBNull.Value,
            status == AgentRunStatus.Failed && failure is not null ? failure : DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public bool StartIfQueued(string id)
    {
        using var cmd = Command(
            "UPDATE agent_runs SET status = $2 WHERE id = $1 AND status = 'queued'",
            id, AgentRunStatus.Running);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <inheritdoc />
    public void Dispose() => source.Dispose();

    private static List<IAgentRun> Read(NpgsqlCommand cmd)
    {
        var runs = new List<IAgentRun>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            runs.Add(new Row
            {
                Id = r.GetString(0),
                Owner = r.GetString(1),
                Project = r.GetString(2),
                Model = r.GetString(3),
                Status = r.GetString(4),
                Started = r.GetFieldValue<DateTimeOffset>(5),
                Ended = r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6),
                InputTokens = r.GetInt64(7),
                OutputTokens = r.GetInt64(8),
                CacheWriteTokens = r.GetInt64(9),
                CacheReadTokens = r.GetInt64(10),
                CostUsd = r.GetDecimal(11),
                Failure = r.IsDBNull(12) ? null : r.GetString(12),
                WorkItem = r.IsDBNull(13) ? null : r.GetString(13),
            });
        }

        return runs;
    }

    /// <summary>A row read back; this layer does not reference the data models, so it carries its own.</summary>
    private sealed class Row : IAgentRun
    {
        public required string Id { get; init; }

        public required string Owner { get; init; }

        public required string Project { get; init; }

        public required string Model { get; init; }

        public required string Status { get; init; }

        public required DateTimeOffset Started { get; init; }

        public DateTimeOffset? Ended { get; init; }

        public long InputTokens { get; init; }

        public long OutputTokens { get; init; }

        public long CacheWriteTokens { get; init; }

        public long CacheReadTokens { get; init; }

        public decimal CostUsd { get; init; }

        public string? Failure { get; init; }

        public string? WorkItem { get; init; }
    }

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
