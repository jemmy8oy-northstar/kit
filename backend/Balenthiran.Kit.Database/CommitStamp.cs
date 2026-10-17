using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <inheritdoc cref="ICommitStamp"/>
public sealed class CommitStamp : ICommitStamp
{
    public required string Commit { get; init; }

    public required DateTimeOffset At { get; init; }
}
