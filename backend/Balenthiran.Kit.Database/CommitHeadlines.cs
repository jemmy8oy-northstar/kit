using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <inheritdoc cref="ICommitHeadlines"/>
public sealed class CommitHeadlines : ICommitHeadlines
{
    public required string? Reason { get; init; }

    public required IReadOnlyList<ICommitHeadline> Commits { get; init; }

    /// <summary>A history that could not be read, and why.</summary>
    public static CommitHeadlines None(string reason) => new() { Reason = reason, Commits = [] };
}
