namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>What reading a project's recent commits found: them newest first, or why there is no history to read.</summary>
public interface ICommitHeadlines
{
    /// <summary>Why the history could not be read. Null when it was — even when it has no commits.</summary>
    string? Reason { get; }

    IReadOnlyList<ICommitHeadline> Commits { get; }
}
