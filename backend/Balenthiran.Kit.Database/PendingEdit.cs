using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <summary><see cref="IPendingEdit"/>, as <see cref="GitHubPullRequestOpener"/> reads it back.</summary>
public sealed record PendingEdit : IPendingEdit
{
    public required string Sha { get; init; }

    public required string Summary { get; init; }

    public string? App { get; init; }

    public string? Behaviour { get; init; }

    public string? When { get; init; }
}
