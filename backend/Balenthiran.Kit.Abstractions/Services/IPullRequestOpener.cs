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
    /// </summary>
    Task<IPullRequestResult> OpenAsync(string head, string baseBranch, string title, string body, CancellationToken cancellationToken = default);
}
