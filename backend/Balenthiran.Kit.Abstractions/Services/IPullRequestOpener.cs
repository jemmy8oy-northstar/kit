using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Proposes a branch Kit has pushed to as a pull request (kit#147, the half of kit#92's
/// Commit button that holds whichever branch shape he picks). Nothing reaches <c>dev</c>
/// except through a pull request he merges (kit#118), so this is the only door out.
/// </summary>
public interface IPullRequestOpener
{
    /// <summary>
    /// Open a pull request from <paramref name="head"/> into <paramref name="baseBranch"/>, or
    /// return the one already open between them. Never throws for a GitHub or network
    /// failure: every outcome without a pull request carries a reason naming the layer.
    /// <paramref name="repository"/> (<c>owner/name</c>) is a project's own repository
    /// (BEH-USERS-12); null is the clone's.
    /// </summary>
    Task<IPullRequestResult> OpenAsync(string head, string baseBranch, string title, string body, string? repository = null, CancellationToken cancellationToken = default);
}

/// <summary>Merges a pull request the pipeline opened, once its checks have passed (BEH-ORCH-10).</summary>
public interface IPullRequestMerger
{
    /// <summary>
    /// Merge the pull request at <paramref name="pullRequestUrl"/> when every Actions run at its head commit
    /// has succeeded (and there is at least one) and GitHub says it is mergeable, merging exactly that commit.
    /// Still-running checks or an unknown mergeability are waiting; a failed run, a conflict, a closed pull
    /// request or unreadable checks are refused with a reason. Never throws for a GitHub or network failure.
    /// <paramref name="repository"/> (<c>owner/name</c>) is used only when the URL does not name one.
    /// With <paramref name="updateFirst"/> (BEH-ORCH-11, slice 7), a pull request whose branch is behind its base is first
    /// brought up to date and waited on, so its checks run on what would be merged; one that cannot be updated is a conflict.
    /// With <paramref name="updateFirst"/> on a private repo (BEH-ORCH-12) no successful Actions run is required, because CI does not run
    /// for it: a pull request that is otherwise ready answers <c>NeedsTest</c> with its head commit until <paramref name="testedSha"/>
    /// (the commit a Kit test run passed) is that head, and is then merged. A failed or running Actions run still refuses or waits.
    /// </summary>
    Task<IPullRequestMerge> MergeIfGreenAsync(string pullRequestUrl, string? repository, CancellationToken cancellationToken, bool updateFirst = false, string? testedSha = null);
}
