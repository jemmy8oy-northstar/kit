using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <inheritdoc cref="ICommitHeadline"/>
public sealed class CommitHeadline : ICommitHeadline
{
    public required string Commit { get; init; }

    public required DateTimeOffset At { get; init; }

    public required string Subject { get; init; }
}
