namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One commit that changed a corpus file.</summary>
public interface ICommitStamp
{
    string Commit { get; }

    /// <summary>The committer date: when the change landed on this branch, not when it was first written.</summary>
    DateTimeOffset At { get; }
}
