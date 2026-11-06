using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <see cref="IAgentRunStore"/> for a Kit with no <c>KIT_DATABASE</c>. ⚠️ Runs live in this process
/// only, so a restart forgets them and their tokens stop working; only the Postgres store is meant for the deployed Kit.
/// </summary>
public sealed class MemoryAgentRunStore : IAgentRunStore
{
    private readonly object gate = new();
    private readonly List<AgentRun> runs = [];
    private readonly Dictionary<string, string> hashes = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IAgentRun Create(string owner, string project, string model, string tokenHash, DateTimeOffset started)
    {
        var run = new AgentRun
        {
            Id = Guid.NewGuid().ToString("N"),
            Owner = owner,
            Project = project,
            Model = model,
            Status = AgentRunStatus.Queued,
            Started = started,
        };
        lock (gate)
        {
            runs.Add(run);
            hashes[tokenHash] = run.Id;
        }

        return run;
    }

    /// <inheritdoc />
    public IReadOnlyList<IAgentRun> List()
    {
        lock (gate)
        {
            return runs.AsEnumerable().Reverse().ToList();
        }
    }

    /// <inheritdoc />
    public IAgentRun? ByTokenHash(string tokenHash)
    {
        lock (gate)
        {
            return hashes.TryGetValue(tokenHash, out var id) ? runs.Find(r => r.Id == id) : null;
        }
    }

    /// <inheritdoc />
    public IAgentRun? AddUsage(string id, AgentUsage usage, decimal costUsd)
    {
        lock (gate)
        {
            var at = runs.FindIndex(r => r.Id == id);
            if (at < 0)
            {
                return null;
            }

            var r = runs[at];
            return runs[at] = With(r, r.Status, r.Ended, usage, costUsd);
        }
    }

    /// <inheritdoc />
    public void SetStatus(string id, string status, DateTimeOffset? ended = null, string? failure = null)
    {
        lock (gate)
        {
            var at = runs.FindIndex(r => r.Id == id);
            if (at >= 0)
            {
                runs[at] = With(runs[at], status, AgentRunStatus.IsOver(status) ? ended : null, default, 0m,
                    status == AgentRunStatus.Failed ? failure : null);
            }
        }
    }

    /// <inheritdoc />
    public bool StartIfQueued(string id)
    {
        lock (gate)
        {
            var at = runs.FindIndex(r => r.Id == id);
            if (at < 0 || runs[at].Status != AgentRunStatus.Queued)
            {
                return false;
            }

            runs[at] = With(runs[at], AgentRunStatus.Running, null, default, 0m);
            return true;
        }
    }

    private static AgentRun With(AgentRun r, string status, DateTimeOffset? ended, AgentUsage add, decimal cost, string? failure = null) => new()
    {
        Id = r.Id,
        Owner = r.Owner,
        Project = r.Project,
        Model = r.Model,
        Status = status,
        Started = r.Started,
        Ended = ended,
        InputTokens = r.InputTokens + add.Input,
        OutputTokens = r.OutputTokens + add.Output,
        CacheWriteTokens = r.CacheWriteTokens + add.CacheWrite,
        CacheReadTokens = r.CacheReadTokens + add.CacheRead,
        CostUsd = r.CostUsd + cost,
        Failure = failure ?? r.Failure,
    };
}
