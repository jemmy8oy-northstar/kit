using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// BEH-USERS-9: where a user's agent runs are recorded, with what each has spent. Keyed by id, and found
/// from a model call by the SHA-256 of its run token, so the token itself is never kept.
/// </summary>
public interface IAgentRunStore
{
    /// <summary>
    /// Records a new queued run; <paramref name="workItem"/> is the slug of the item a pipeline run is for, null
    /// otherwise, and <paramref name="stage"/> the pipeline stage it runs (BEH-ORCH-9), null for a run started by hand.
    /// </summary>
    IAgentRun Create(string owner, string project, string model, string tokenHash, DateTimeOffset started, string? workItem = null, string? stage = null);

    /// <summary>Every run, newest start first (BEH-UI-7).</summary>
    IReadOnlyList<IAgentRun> List();

    /// <summary>The run a token's SHA-256 (lower-case hex) belongs to; null when none.</summary>
    IAgentRun? ByTokenHash(string tokenHash);

    /// <summary>Adds one call's tokens and cost to the run, atomically; null when the run does not exist.</summary>
    IAgentRun? AddUsage(string id, AgentUsage usage, decimal costUsd);

    /// <summary>Moves the run to <paramref name="status"/>; an over status stamps <paramref name="ended"/>; <paramref name="failure"/> is why a failed run failed, kept only for a failed status.</summary>
    void SetStatus(string id, string status, DateTimeOffset? ended = null, string? failure = null);

    /// <summary>
    /// Moves the run from queued to running, atomically, and only if it is still queued, so a run that became
    /// finished, failed or refused meanwhile is never brought back. True when it moved.
    /// </summary>
    bool StartIfQueued(string id);
}
