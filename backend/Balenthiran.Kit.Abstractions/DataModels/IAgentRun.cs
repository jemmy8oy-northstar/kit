namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// BEH-USERS-9: one run of an agent Kit runs for a user. The run token that authenticates it is never
/// part of this, and is not stored: only its SHA-256 is, in the run store.
/// </summary>
public interface IAgentRun
{
    /// <summary>Kit's id for the run.</summary>
    string Id { get; }

    /// <summary>The signed-in account that owns the run (the operator's is <c>ISessionStore.Operator</c>).</summary>
    string Owner { get; }

    /// <summary>The project the run works on.</summary>
    string Project { get; }

    /// <summary>The model the run was asked to use.</summary>
    string Model { get; }

    /// <summary>One of <see cref="Balenthiran.Kit.Abstractions.Services.AgentRunStatus"/>.</summary>
    string Status { get; }

    /// <summary>When the run was created.</summary>
    DateTimeOffset Started { get; }

    /// <summary>When it finished, failed or was refused; null while it is queued or running.</summary>
    DateTimeOffset? Ended { get; }

    /// <summary>Uncached input tokens the run has used.</summary>
    long InputTokens { get; }

    /// <summary>Output tokens the run has used.</summary>
    long OutputTokens { get; }

    /// <summary>Tokens written to the prompt cache (5 minute and 1 hour together).</summary>
    long CacheWriteTokens { get; }

    /// <summary>Tokens read back from the prompt cache.</summary>
    long CacheReadTokens { get; }

    /// <summary>What the run's model calls have cost so far, USD at list price.</summary>
    decimal CostUsd { get; }

    /// <summary>Why a failed run failed, as its runner reported it: a step and an exit code; null otherwise.</summary>
    string? Failure { get; }

    /// <summary>The work-item slug a pipeline run was for (BEH-ORCH-6); null for a run not started for one item, such as triage.</summary>
    string? WorkItem { get; }

    /// <summary>The pipeline stage a run was for (triage, spec, build or review; BEH-ORCH-9); null for a run started by hand.</summary>
    string? Stage { get; }
}

/// <summary>The body of <c>GET /api/runs</c>: runs newest first and the cap each is held to.</summary>
public interface IAgentRunList
{
    /// <summary>What one run may spend, USD (<c>KIT_AGENT_RUN_CAP_USD</c>).</summary>
    decimal CapUsd { get; }

    /// <summary>Newest first.</summary>
    IReadOnlyList<IAgentRun> Runs { get; }

    /// <summary>
    /// BEH-ORCH-17: the runs a runner has taken from the pool and not yet reported, with when it took each. Kept in
    /// memory only, so a run taken before Kit last started is not here. Empty without a runner pool.
    /// </summary>
    IReadOnlyList<IRunClaim> Claims { get; }
}

/// <summary>BEH-ORCH-17: a run a runner has taken and not yet reported, and when Kit will count that runner lost.</summary>
public interface IRunClaim
{
    /// <summary>The run's id, as in <see cref="IAgentRun.Id"/>.</summary>
    string RunId { get; }

    /// <summary>When a runner took the run from the pool: the moment it began to run.</summary>
    DateTimeOffset ClaimedAt { get; }

    /// <summary>When its runner ends it for running too long: <see cref="ClaimedAt"/> plus its stage's time limit; null when it has none of its own.</summary>
    DateTimeOffset? GivesUpAt { get; }

    /// <summary>When Kit counts its runner lost if it has still not reported (BEH-ORCH-14): <see cref="ClaimedAt"/> plus the lease, which follows the time limit when there is one.</summary>
    DateTimeOffset LostAt { get; }
}

/// <summary>
/// The body of <c>POST /api/projects/&lt;project&gt;/runs</c>: the run, the run token (shown this once and
/// never again) and what the launcher said.
/// </summary>
public interface IAgentRunStarted
{
    /// <summary>The run as created, after the launcher has had it.</summary>
    IAgentRun Run { get; }

    /// <summary>The secret the run authenticates to Kit's model proxy with. Only its SHA-256 is kept.</summary>
    string Token { get; }

    /// <summary>What the launcher said, e.g. that no runner is configured.</summary>
    string Launch { get; }
}

/// <summary>One work item on <c>GET /api/pipeline</c> (BEH-ORCH-9): where it is, why it stopped, and its flag and review.</summary>
public interface IPipelineItem
{
    string Project { get; }

    string Slug { get; }

    /// <summary>triage, spec, build or review.</summary>
    string Stage { get; }

    /// <summary>queued, running, stopped or done.</summary>
    string Status { get; }

    string? StoppedBecause { get; }

    string? PullRequest { get; }

    /// <summary>Kit merged the pull request (BEH-ORCH-10).</summary>
    bool Merged { get; }

    /// <summary>The bundle's Flag line, without "Flag: "; null when it has none.</summary>
    string? Flag { get; }

    /// <summary>The review's outcome, agreed, revised or escalated; null when it was not reviewed.</summary>
    string? Review { get; }

    string? ReviewNote { get; }

    /// <summary>Why a built item is not merged yet (BEH-ORCH-10): merging is off, or what its pull request still waits for; null otherwise.</summary>
    string? MergeNote { get; }
}

/// <summary>The body of <c>GET /api/pipeline</c>: whether the pipeline is on, and its items since Kit last started, oldest first.</summary>
public interface IPipelineItemList
{
    bool On { get; }

    IReadOnlyList<IPipelineItem> Items { get; }

    /// <summary>BEH-ORCH-21: "Paused: usage limit, resumes 13:00Z" while no run starts for the usage limit; null otherwise.</summary>
    string? Paused { get; }
}
