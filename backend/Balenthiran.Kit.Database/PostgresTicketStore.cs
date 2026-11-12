using Balenthiran.Kit.Abstractions.Services;
using Npgsql;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="ITicketStore"/> in the database Kit runs (<c>KIT_DATABASE</c>), beside the triaged notes: the pipeline's
/// tickets (BEH-ORCH-11), so a restart does not lose them. The table is made on first use, as
/// <see cref="PostgresTriagedNoteStore"/> makes its own.
/// </summary>
public sealed class PostgresTicketStore(string connectionString) : ITicketStore, IDisposable
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS pipeline_tickets (
            project text NOT NULL,
            slug text NOT NULL,
            title text NOT NULL,
            state text NOT NULL,
            priority integer NOT NULL,
            cards text[] NOT NULL DEFAULT '{}',
            bundle text,
            pull_request text,
            stopped_because text,
            scheduled boolean NOT NULL DEFAULT false,
            created timestamptz NOT NULL DEFAULT now(),
            updated timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY (project, slug)
        )
        """;

    private const string Columns = "project, slug, title, state, priority, cards, bundle, pull_request, stopped_because, scheduled";

    private readonly NpgsqlDataSource source = NpgsqlDataSource.Create(connectionString);
    private readonly Lock gate = new();
    private bool made;

    /// <inheritdoc />
    public void Save(PipelineTicket ticket)
    {
        using var cmd = Command(
            "INSERT INTO pipeline_tickets (" + Columns + ") VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10) "
            + "ON CONFLICT (project, slug) DO UPDATE SET title = $3, state = $4, priority = $5, cards = $6, bundle = $7, "
            + "pull_request = $8, stopped_because = $9, scheduled = $10, updated = now()",
            ticket.Project,
            ticket.Slug,
            ticket.Title,
            ticket.State.ToString().ToLowerInvariant(),
            ticket.Priority,
            ticket.Cards.ToArray(),
            ticket.Bundle,
            ticket.PullRequest,
            ticket.StoppedBecause,
            ticket.Scheduled);
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public IReadOnlyList<PipelineTicket> Open() =>
        Query("SELECT " + Columns + " FROM pipeline_tickets WHERE state NOT IN ('merged', 'stopped') ORDER BY created, slug");

    /// <inheritdoc />
    public IReadOnlyList<PipelineTicket> Read(string project) =>
        Query("SELECT " + Columns + " FROM pipeline_tickets WHERE project = $1 ORDER BY created, slug", project);

    /// <inheritdoc />
    public void Dispose() => source.Dispose();

    private List<PipelineTicket> Query(string sql, params object?[] values)
    {
        using var cmd = Command(sql, values);
        using var rows = cmd.ExecuteReader();
        var tickets = new List<PipelineTicket>();
        while (rows.Read())
        {
            tickets.Add(new PipelineTicket(
                rows.GetString(0),
                rows.GetString(1),
                rows.GetString(2),
                Enum.Parse<TicketState>(rows.GetString(3), ignoreCase: true),
                rows.GetInt32(4),
                rows.GetFieldValue<string[]>(5),
                rows.IsDBNull(6) ? null : rows.GetString(6),
                rows.IsDBNull(7) ? null : rows.GetString(7),
                rows.IsDBNull(8) ? null : rows.GetString(8),
                rows.GetBoolean(9)));
        }

        return tickets;
    }

    private NpgsqlCommand Command(string sql, params object?[] values)
    {
        EnsureSchema();
        var cmd = source.CreateCommand(sql);
        foreach (var v in values)
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = v ?? DBNull.Value });
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
