namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// What asking to merge a pull request did (BEH-ORCH-10). Exactly one of the three: merged, still
/// waiting (checks running, or GitHub has not yet worked out mergeability), or refused with a reason.
/// </summary>
public interface IPullRequestMerge
{
    /// <summary>The pull request was merged by this call.</summary>
    bool Merged { get; }

    /// <summary>Not merged and not refused: look again later.</summary>
    bool Waiting { get; }

    /// <summary>
    /// Refused because the pull request's branch conflicts with its base (BEH-ORCH-11, slice 7b), which an agent can resolve;
    /// every other refusal leaves this false. <see cref="Reason"/> is set as for any refusal.
    /// </summary>
    bool Conflict { get; }

    /// <summary>Why the pull request will not be merged, in plain words. Never contains the token.</summary>
    string? Reason { get; }
}
