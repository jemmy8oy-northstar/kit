namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// The body of <c>GET /api/activity</c> (BEH-ACTIVITY-1): the recent commits that touched any
/// project's corpus or notes, newest first. An action is a commit — every edit, approval, note and
/// build already lands as one — so nothing new is stored.
/// </summary>
public interface IActivityFeed
{
    /// <summary>One entry per (commit, project), newest first, capped.</summary>
    IReadOnlyList<IActivityEntry> Entries { get; }

    /// <summary>
    /// Projects whose history this server cannot read — read from GitHub with no local clone, or
    /// outside a work tree. Listed, never dropped: absence of history is not the same as quiet.
    /// </summary>
    IReadOnlyList<IActivityGap> NoHistory { get; }
}
