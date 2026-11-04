using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// BEH-USERS-9: where a user's agent runs are recorded, with what each has spent. Keyed by id, and found
/// from a model call by the SHA-256 of its run token, so the token itself is never kept.
/// </summary>
public interface IAgentRunStore
{
    /// <summary>Records a new queued run.</summary>
    IAgentRun Create(string owner, string project, string model, string tokenHash, DateTimeOffset started);

    /// <summary>Every run, newest first.</summary>
    IReadOnlyList<IAgentRun> List();

    /// <summary>The run a token's SHA-256 (lower-case hex) belongs to; null when none.</summary>
    IAgentRun? ByTokenHash(string tokenHash);

    /// <summary>Adds one call's tokens and cost to the run, atomically; null when the run does not exist.</summary>
    IAgentRun? AddUsage(string id, AgentUsage usage, decimal costUsd);

    /// <summary>Moves the run to <paramref name="status"/>; an over status stamps <paramref name="ended"/>.</summary>
    void SetStatus(string id, string status, DateTimeOffset? ended = null);

    /// <summary>
    /// Moves the run from queued to running, atomically, and only if it is still queued, so a run that became
    /// finished, failed or refused meanwhile is never brought back. True when it moved.
    /// </summary>
    bool StartIfQueued(string id);
}
