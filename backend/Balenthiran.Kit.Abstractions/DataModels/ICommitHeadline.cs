namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One commit that changed a project's corpus or notes, with the subject it was made with (BEH-ACTIVITY-1).</summary>
public interface ICommitHeadline
{
    string Commit { get; }

    /// <summary>The committer date: when the change landed on this branch.</summary>
    DateTimeOffset At { get; }

    /// <summary>The first line of the commit message.</summary>
    string Subject { get; }
}
