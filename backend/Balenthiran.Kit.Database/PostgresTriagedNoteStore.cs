using Balenthiran.Kit.Abstractions.Services;
using Npgsql;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="ITriagedNoteStore"/> in the database Kit runs (<c>KIT_DATABASE</c>), beside the runs: the notes a
/// triage has sorted (BEH-ORCH-8), so a restart neither forgets them nor pays to triage them again. The table is
/// made on first use, as <see cref="PostgresPushStore"/> makes its own.
/// </summary>
public sealed class PostgresTriagedNoteStore(string connectionString) : ITriagedNoteStore, IDisposable
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS pipeline_triaged_notes (
            project text NOT NULL,
            note_key text NOT NULL,
            triaged timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY (project, note_key)
        )
        """;

    private readonly NpgsqlDataSource source = NpgsqlDataSource.Create(connectionString);
    private readonly Lock gate = new();
    private bool made;

    /// <inheritdoc />
    public IReadOnlySet<string> Read(string project)
    {
        using var cmd = Command("SELECT note_key FROM pipeline_triaged_notes WHERE project = $1", project);
        using var rows = cmd.ExecuteReader();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        while (rows.Read())
        {
            keys.Add(rows.GetString(0));
        }

        return keys;
    }

    /// <inheritdoc />
    public void Add(string project, IReadOnlyCollection<string> keys)
    {
        if (keys.Count == 0)
        {
            return;
        }

        // One statement, so a triage's notes are recorded together or not at all.
        using var cmd = Command(
            "INSERT INTO pipeline_triaged_notes (project, note_key) SELECT $1, k FROM unnest($2::text[]) AS k ON CONFLICT DO NOTHING",
            project,
            keys.ToArray());
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Projects()
    {
        using var cmd = Command("SELECT DISTINCT project FROM pipeline_triaged_notes ORDER BY project");
        using var rows = cmd.ExecuteReader();
        var projects = new List<string>();
        while (rows.Read())
        {
            projects.Add(rows.GetString(0));
        }

        return projects;
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
