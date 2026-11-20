using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <summary><see cref="IPullRequestMerge"/>, as <see cref="GitHubPullRequestOpener"/> reports it.</summary>
public sealed record PullRequestMerge : IPullRequestMerge
{
    public bool Merged { get; init; }

    public bool Waiting { get; init; }

    public bool Conflict { get; init; }

    public bool NeedsTest { get; init; }

    public string? HeadSha { get; init; }

    public string? Reason { get; init; }
}
