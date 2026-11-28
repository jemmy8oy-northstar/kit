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
    private readonly Dictionary<string, string> repos = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IAgentRun Create(string owner, string project, string model, string tokenHash, DateTimeOffset started, string? workItem = null, string? stage = null)
    {
        var run = new AgentRun
        {
            WorkItem = workItem,
            Stage = stage,
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
            // By start, newest first, as the Postgres store orders it (BEH-UI-7); a tie keeps the later-recorded run first.
            return runs.AsEnumerable().Reverse().OrderByDescending(r => r.Started).ToList();
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
    public bool StartIfQueued(string id, string? repo = null)
    {
        lock (gate)
        {
            var at = runs.FindIndex(r => r.Id == id);
            if (at < 0 || runs[at].Status != AgentRunStatus.Queued)
            {
                return false;
            }

            runs[at] = With(runs[at], AgentRunStatus.Running, null, default, 0m);
            if (repo is not null)
            {
                repos[id] = repo;
            }

            return true;
        }
    }

    /// <inheritdoc />
    public string? RepoOf(string id)
    {
        lock (gate)
        {
            return repos.GetValueOrDefault(id);
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
        WorkItem = r.WorkItem,
        Stage = r.Stage,
    };
}
