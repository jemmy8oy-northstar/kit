using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Reads ONE file through GitHub's REST API, with the same credential and the same reasons as
/// <see cref="IGitHubCorpusReader"/> (BEH-NOTIFY-1 reads the agent's feed so).
/// </summary>
public interface IGitHubFileReader
{
    /// <summary>
    /// The text of the file <paramref name="file"/>'s path names, and its ETag. Pass the ETag of the
    /// last read and an unchanged file answers null, for one 304. Throws
    /// <see cref="Exceptions.GitHubReadException"/> with a reason a reader may be shown.
    /// </summary>
    Task<(string Text, string? ETag)?> ReadFileAsync(IProjectSource file, string? etag = null, CancellationToken cancellationToken = default);
}
