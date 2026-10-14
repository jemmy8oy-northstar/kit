namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>What reading a corpus file's history found: its commits oldest first, or why there are none.</summary>
public interface ICommitLog
{
    /// <summary>Why the history could not be read. Null when it was — even when it has no commits yet.</summary>
    string? Reason { get; }

    IReadOnlyList<ICommitStamp> Commits { get; }
}
