namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Asks GitHub whether a repository is public (kit-hosted.beh BEH-GATE-H1, kit#795): the one fact
/// that decides whether a project's spec may be served to a caller who has not signed in.
/// </summary>
public interface IGitHubRepoReader
{
    /// <summary>
    /// True only when GitHub answers that <paramref name="repo"/> (<c>owner/repo</c>, an
    /// <c>@branch</c> suffix ignored) is not private. Throws
    /// <see cref="Exceptions.GitHubReadException"/> when it cannot tell, which the caller treats as private.
    /// </summary>
    Task<bool> IsPublicAsync(string repo, CancellationToken cancellationToken = default);
}
