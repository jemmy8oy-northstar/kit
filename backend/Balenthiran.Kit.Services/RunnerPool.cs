using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-USERS-9's runner pool: a fixed set of runner pods ask Kit for the next queued run, so Kit needs no
/// cluster access and never starts anything. Queued runs wait here IN MEMORY ONLY: the run token and the task
/// are never stored (the store keeps the token's SHA-256), so a restart forgets what was waiting and those runs
/// stay queued until the operator starts them again. One live run per project: a run waits while another on its
/// project has been handed out and is not over; other projects are not held up.
/// </summary>
public sealed class RunnerPool(IAgentRunStore store) : IAgentRunLauncher
{
    /// <summary>What the operator is told when a run went to the pool.</summary>
    public const string QueuedNote = "queued for the runner pool";

    private sealed record Item(string RunId, string Token, string Repo, string Task, string? BaseBranch, int? MaxTurns);

    private readonly object gate = new();
    private readonly List<Item> queue = [];
    private readonly Dictionary<string, string> handedOut = new(StringComparer.Ordinal);
    // Run id -> repo for runs handed out and not yet over, so Kit knows which ONE repo a run's GitHub token may
    // reach. Memory only, like the queue: a restart forgets it and the run's next token request is refused.
    private readonly Dictionary<string, string> repoByRun = new(StringComparer.Ordinal);
    private TaskCompletionSource signal = NewSignal();

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>A pool run needs a repository and a task, which the interface cannot carry: use <see cref="Enqueue"/>.</summary>
    public Task<AgentLaunch> LaunchAsync(IAgentRun run, string runToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AgentLaunch(false, "the runner pool needs a repo and a task to run, so the run stays queued"));

    /// <summary>
    /// Holds <paramref name="run"/> for the next runner that asks. Memory only. <paramref name="maxTurns"/> (BEH-ORCH-5)
    /// is the run's turn limit; none means the runner keeps its own.
    /// </summary>
    public void Enqueue(IAgentRun run, string token, string repo, string task, string? baseBranch, int? maxTurns = null)
    {
        lock (gate)
        {
            queue.Add(new Item(run.Id, token, repo, task, baseBranch, maxTurns));
        }

        Signal();
    }

    /// <summary>Wakes a waiting claim: a run was queued, or one ended and may have freed its project.</summary>
    public void Signal()
    {
        TaskCompletionSource old;
        lock (gate)
        {
            old = signal;
            signal = NewSignal();
        }

        old.TrySetResult();
    }

    /// <summary>
    /// The oldest queued run that may start now, moved to running; null after waiting up to
    /// <paramref name="wait"/> for one to become claimable.
    /// </summary>
    public async Task<PoolClaim?> ClaimAsync(TimeSpan wait, CancellationToken cancellationToken = default)
    {
        Task changed;
        lock (gate)
        {
            changed = signal.Task;
        }

        if (TryClaim() is { } now)
        {
            return now;
        }

        if (wait <= TimeSpan.Zero)
        {
            return null;
        }

        try
        {
            await changed.WaitAsync(wait, cancellationToken);
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return TryClaim();
    }

    private PoolClaim? TryClaim()
    {
        lock (gate)
        {
            // A project is free again once the run handed out on it is over (or has gone from the store).
            foreach (var (project, runId) in handedOut.ToList())
            {
                if (OverOrGone(runId))
                {
                    handedOut.Remove(project);
                }
            }

            foreach (var runId in repoByRun.Keys.ToList())
            {
                if (OverOrGone(runId))
                {
                    repoByRun.Remove(runId);
                }
            }

            for (var i = 0; i < queue.Count; i++)
            {
                var item = queue[i];
                var run = Find(item.RunId);
                if (run is null || run.Status != AgentRunStatus.Queued)
                {
                    queue.RemoveAt(i--);
                    continue;
                }

                if (handedOut.ContainsKey(run.Project))
                {
                    continue;
                }

                queue.RemoveAt(i--);
                if (!store.StartIfQueued(run.Id))
                {
                    continue;
                }

                handedOut[run.Project] = run.Id;
                repoByRun[run.Id] = item.Repo;
                return new PoolClaim(run.Id, item.Token, item.Repo, item.Task, item.BaseBranch, run.Model, item.MaxTurns);
            }

            return null;
        }
    }

    /// <summary>The repo of a run this pool handed out that is not over, else null (never handed out, over, or forgotten at a restart).</summary>
    public string? RepoOf(string runId)
    {
        lock (gate)
        {
            if (!repoByRun.TryGetValue(runId, out var repo))
            {
                return null;
            }

            if (OverOrGone(runId))
            {
                repoByRun.Remove(runId);
                return null;
            }

            return repo;
        }
    }

    private IAgentRun? Find(string id) => store.List().FirstOrDefault(r => r.Id == id);

    private bool OverOrGone(string id) => Find(id) is not { } r || AgentRunStatus.IsOver(r.Status);
}
