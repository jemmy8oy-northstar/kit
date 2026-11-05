using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Commits one corpus file into an <see cref="IProjectSource"/>'s own repository through GitHub's
/// Contents API (BEH-USERS-12), with the same token the reader uses.
/// </summary>
public interface IGitHubCorpusWriter
{
    /// <summary>
    /// Replace (or create, when <paramref name="sha"/> is null) <paramref name="fileName"/> in the
    /// source's directory on its branch, as one commit. <paramref name="sha"/> is the blob Kit last
    /// read, so a file someone else changed first is refused, never overwritten. Answers the new
    /// blob's sha and the commit's. Throws <see cref="Exceptions.GitHubWriteException"/> with
    /// GitHub's reason; <see cref="Exceptions.GitHubWriteException.Stale"/> says it was that refusal.
    /// </summary>
    Task<(string Blob, string Commit)> PutAsync(IProjectSource source, string fileName, string text, string? sha, string message, CancellationToken cancellationToken = default);
}
