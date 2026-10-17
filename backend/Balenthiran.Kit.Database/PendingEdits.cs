using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <summary><see cref="IPendingEdits"/>, as <see cref="GitHubPullRequestOpener"/> reports it.</summary>
public sealed record PendingEdits : IPendingEdits
{
    public IReadOnlyList<IPendingEdit> Edits { get; init; } = [];

    public int? PullNumber { get; init; }

    public string? PullUrl { get; init; }

    public string? Reason { get; init; }
}
