using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <summary><see cref="IPullRequestResult"/>, as <see cref="GitHubPullRequestOpener"/> reports it.</summary>
public sealed record PullRequestResult : IPullRequestResult
{
    public bool Opened { get; init; }

    public bool AlreadyOpen { get; init; }

    public int? Number { get; init; }

    public string? Url { get; init; }

    public string? Reason { get; init; }
}
