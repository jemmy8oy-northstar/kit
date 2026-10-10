using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Reads one <see cref="IProjectSource"/>'s corpora through GitHub's REST API (BEH-PULL-1..3),
/// with the App token when one is configured and without one otherwise — a public repository
/// needs none (BEH-PULL-2).
/// </summary>
public interface IGitHubCorpusReader
{
    /// <summary>
    /// The directory now. Pass the previous snapshot of the same source and an unchanged directory
    /// costs one 304, and a changed one fetches only the files whose sha moved. Throws
    /// <see cref="Exceptions.GitHubReadException"/> rather than answer with fewer files than exist.
    /// </summary>
    Task<ICorpusSnapshot> ReadAsync(IProjectSource source, ICorpusSnapshot? previous = null, CancellationToken cancellationToken = default);
}
