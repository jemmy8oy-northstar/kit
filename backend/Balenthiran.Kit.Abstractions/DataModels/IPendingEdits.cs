namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// What is waiting to reach the base (BEH-COMMIT-5): the edits branch's commits the base does not
/// have, newest first, and the Commit pull request they wait in when one is open.
/// </summary>
public interface IPendingEdits
{
    IReadOnlyList<IPendingEdit> Edits { get; }

    /// <summary>The open pull request from the edits branch into the base, or null when none is open.</summary>
    int? PullNumber { get; }

    string? PullUrl { get; }

    /// <summary>
    /// Set when Kit could not find out — GitHub's own words where GitHub spoke. An edits branch that
    /// does not exist yet is not a reason: it is simply nothing pending.
    /// </summary>
    string? Reason { get; }
}
