using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-USERS-9's runner pool: a fixed set of runner pods ask Kit for the next queued run, so Kit needs no
/// cluster access and never starts anything. Queued runs wait here IN MEMORY ONLY: the run token and the task
/// are never stored (the store keeps the token's SHA-256), so a restart forgets what was waiting, and those runs are
/// failed at the first claim after it (<see cref="Forgotten"/>). One live run per project: a run waits while another on its
/// project has been handed out and is not over; other projects are not held up.
/// </summary>
public sealed class RunnerPool(IAgentRunStore store, TimeProvider? clock = null) : IAgentRunLauncher
{
    /// <summary>What the operator is told when a run went to the pool.</summary>
    public const string QueuedNote = "queued for the runner pool";

    /// <summary>
    /// BEH-ORCH-14: how long after it was handed out a run may go without reporting before Kit counts its runner lost.
    /// The runner ends its agent at KIT_RUN_MAX_SECONDS (6600) and its claimer gives up at two hours, so a run still
    /// silent at two and a half has a runner that died without a word (its pod killed or evicted), and nothing else
    /// would ever free its project.
    /// </summary>
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(150);

    /// <summary>
    /// BEH-ORCH-17: for a run with its own time limit, how long past that limit Kit waits for its report before counting
    /// its runner lost. The runner ends the agent at the limit and reports at once, so ten minutes covers a slow report.
    /// </summary>
    public static readonly TimeSpan LeaseMargin = TimeSpan.FromMinutes(10);

    /// <summary>The lease of a run with time limit <paramref name="maxSeconds"/>: the limit plus <see cref="LeaseMargin"/>, or <see cref="Lease"/> with none.</summary>
    public static TimeSpan LeaseFor(int? maxSeconds) => maxSeconds is { } s ? TimeSpan.FromSeconds(s) + LeaseMargin : Lease;

    /// <summary>The failure a lost run is recorded with (BEH-ORCH-14).</summary>
    public const string LostFailure = "lost";

    private sealed record Item(string RunId, string Token, string Repo, string Task, string? BaseBranch, int? MaxTurns, int? MaxSeconds);

    private readonly object gate = new();
    private readonly List<Item> queue = [];
    private readonly Dictionary<string, string> handedOut = new(StringComparer.Ordinal);
    // Run id -> repo for runs handed out and not yet over, so Kit knows which ONE repo a run's GitHub token may
    // reach. The store keeps it too (BEH-ORCH-8), so after a restart RepoOf answers from there.
    private readonly Dictionary<string, string> repoByRun = new(StringComparer.Ordinal);
    // Run id -> when it was handed out, for the lease (BEH-ORCH-14). Memory only, like repoByRun, and cleared with it.
    private readonly Dictionary<string, DateTimeOffset> handedOutAt = new(StringComparer.Ordinal);
    // Run id -> its time limit in seconds, null for none (BEH-ORCH-17). Kept and cleared with handedOutAt.
    private readonly Dictionary<string, int?> limitOf = new(StringComparer.Ordinal);
    // BEH-ORCH-17: when this pool began, and the runs still going then that a restart forgot (see Forgotten).
    private readonly DateTimeOffset bornAt = (clock ?? TimeProvider.System).GetUtcNow();
    private readonly HashSet<string> adopted = new(StringComparer.Ordinal);
    private bool lookedForForgotten;
    private TaskCompletionSource signal = NewSignal();

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>A pool run needs a repository and a task, which the interface cannot carry: use <see cref="Enqueue"/>.</summary>
    public Task<AgentLaunch> LaunchAsync(IAgentRun run, string runToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AgentLaunch(false, "the runner pool needs a repo and a task to run, so the run stays queued"));

    /// <summary>
    /// Holds <paramref name="run"/> for the next runner that asks. Memory only. <paramref name="maxTurns"/> (BEH-ORCH-5)
    /// is the run's turn limit and <paramref name="maxSeconds"/> (BEH-ORCH-17) its time limit; none means the runner
    /// keeps its own.
    /// </summary>
    public void Enqueue(IAgentRun run, string token, string repo, string task, string? baseBranch, int? maxTurns = null, int? maxSeconds = null)
    {
        lock (gate)
        {
            queue.Add(new Item(run.Id, token, repo, task, baseBranch, maxTurns, maxSeconds));
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
                    handedOutAt.Remove(runId);
                    limitOf.Remove(runId);
                }
            }

            // BEH-ORCH-20: a test or merge run goes first, oldest first, then everything else. With one runner, a test queued
            // behind another project's build waited up to two hours, and every built pull request waited with it.
            return Take(true) ?? Take(false);
        }
    }

    // The oldest queued run whose project is free, test and merge runs only when firstOnly. Called under gate.
    private PoolClaim? Take(bool firstOnly)
    {
        for (var i = 0; i < queue.Count; i++)
        {
            var item = queue[i];
            var run = Find(item.RunId);
            if (run is null || run.Status != AgentRunStatus.Queued)
            {
                queue.RemoveAt(i--);
                continue;
            }

            if (handedOut.ContainsKey(run.Project) || (firstOnly && !GoesFirst(run.Stage)))
            {
                continue;
            }

            queue.RemoveAt(i--);
            if (!store.StartIfQueued(run.Id, item.Repo))
            {
                continue;
            }

            handedOut[run.Project] = run.Id;
            repoByRun[run.Id] = item.Repo;
            handedOutAt[run.Id] = Now;
            limitOf[run.Id] = item.MaxSeconds;
            return new PoolClaim(run.Id, item.Token, item.Repo, item.Task, item.BaseBranch, run.Model, item.MaxTurns, run.Stage, item.MaxSeconds);
        }

        return null;
    }

    /// <summary>BEH-ORCH-20: the stages a runner takes before any other: a test run, and a merge run resolving a conflict.</summary>
    public static bool GoesFirst(string? stage) => stage is "test" or "resolve" or "fix";

    /// <summary>
    /// The repo of a handed-out run that is not over, else null (never handed out, or over). BEH-ORCH-8: a run handed out
    /// before a restart is answered from the store, which kept its repo when it started, so its push still works.
    /// </summary>
    public string? RepoOf(string runId)
    {
        lock (gate)
        {
            if (!repoByRun.TryGetValue(runId, out var repo))
            {
                return Find(runId) is { Status: AgentRunStatus.Running } ? store.RepoOf(runId) : null;
            }

            if (OverOrGone(runId))
            {
                repoByRun.Remove(runId);
                handedOutAt.Remove(runId);
                limitOf.Remove(runId);
                return null;
            }

            return repo;
        }
    }

    /// <summary>
    /// BEH-ORCH-17: each run this pool handed out that is not over, with when a runner took it, when its runner ends it
    /// (null with no limit of its own) and when Kit counts its runner lost, oldest first. What a restart forgot is not
    /// here, as for <see cref="Lost"/>.
    /// </summary>
    public IReadOnlyList<(string RunId, DateTimeOffset ClaimedAt, DateTimeOffset? GivesUpAt, DateTimeOffset LostAt)> Claimed()
    {
        lock (gate)
        {
            return handedOutAt
                .Where(h => !adopted.Contains(h.Key) && !OverOrGone(h.Key))
                .OrderBy(h => h.Value)
                .Select(h =>
                {
                    var limit = limitOf.GetValueOrDefault(h.Key);
                    return (h.Key, h.Value, limit is { } s ? h.Value + TimeSpan.FromSeconds(s) : (DateTimeOffset?)null, h.Value + LeaseFor(limit));
                })
                .ToList();
        }
    }

    /// <summary>
    /// BEH-ORCH-14: the runs this pool handed out at least their lease (<see cref="LeaseFor"/>) ago that are still not over, oldest
    /// first. A run is counted lost at its lost-at time, not after it. Their runners are lost; the caller fails them, which frees their projects. A run handed out before a
    /// restart is not here: the restart already forgot it, and its project with it. Each is given out once, so two
    /// claims at the same moment cannot both fail it.
    /// </summary>
    public IReadOnlyList<string> Lost()
    {
        lock (gate)
        {
            var now = Now;
            var lost = handedOutAt
                .Where(h => now - h.Value >= LeaseFor(limitOf.GetValueOrDefault(h.Key)) && !OverOrGone(h.Key))
                .OrderBy(h => h.Value)
                .Select(h => h.Key)
                .ToList();
            foreach (var id in lost)
            {
                handedOutAt.Remove(id);
            }

            return lost;
        }
    }

    /// <summary>Gives back a run from <see cref="Lost"/> that could not be failed, so the next <see cref="Lost"/> returns it again.</summary>
    public void StillLost(string runId)
    {
        lock (gate)
        {
            if (repoByRun.ContainsKey(runId) || adopted.Contains(runId))
            {
                handedOutAt[runId] = DateTimeOffset.MinValue;
            }
        }
    }

    /// <summary>
    /// BEH-ORCH-17: the runs a restart forgot, looked for once. A run queued before this pool began can never be handed
    /// out (its token went with the restart), so it is returned for the caller to fail. A run already going then may
    /// still have a runner that reports, so it is not failed now: it gets the lease from when this pool began (one made more than a lease earlier goes at once), and
    /// <see cref="Lost"/> gives it out once that passes, as for any run. Without this, both stayed going for ever.
    /// </summary>
    public IReadOnlyList<string> Forgotten()
    {
        lock (gate)
        {
            if (lookedForForgotten)
            {
                return [];
            }

            var before = store.List().Where(r => r.Started < bornAt && !AgentRunStatus.IsOver(r.Status)).ToList();
            lookedForForgotten = true;
            foreach (var r in before.Where(r => r.Status == AgentRunStatus.Running && !handedOutAt.ContainsKey(r.Id)))
            {
                adopted.Add(r.Id);
                // BEH-ORCH-19: a run made more than a lease before this pool began goes at once, else every restart (each release
                // is one) gave a run days old a fresh lease. Started is when it was made, not taken, so a younger one keeps the restart's.
                handedOutAt[r.Id] = bornAt - r.Started > Lease ? r.Started : bornAt;
                limitOf[r.Id] = null;
            }

            return before.Where(r => r.Status == AgentRunStatus.Queued && queue.All(i => i.RunId != r.Id)).Select(r => r.Id).ToList();
        }
    }

    private DateTimeOffset Now => (clock ?? TimeProvider.System).GetUtcNow();

    private IAgentRun? Find(string id) => store.List().FirstOrDefault(r => r.Id == id);

    private bool OverOrGone(string id) => Find(id) is not { } r || AgentRunStatus.IsOver(r.Status);
}
