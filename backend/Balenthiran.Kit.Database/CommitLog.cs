using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <inheritdoc cref="ICommitLog"/>
public sealed class CommitLog : ICommitLog
{
    public required string? Reason { get; init; }

    public required IReadOnlyList<ICommitStamp> Commits { get; init; }

    /// <summary>A history that could not be read, and why.</summary>
    public static CommitLog None(string reason) => new() { Reason = reason, Commits = [] };
}
