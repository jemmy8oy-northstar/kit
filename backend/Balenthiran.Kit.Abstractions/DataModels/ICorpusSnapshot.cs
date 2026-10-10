namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// The corpus files of one <see cref="IProjectSource"/> as GitHub served them at one moment.
/// Handed back on the next read, it lets that read be a conditional request: an unchanged
/// directory answers 304 and nothing is re-fetched. ⚠️ A 304 is free of the rate limit only when
/// the request carries a token: measured 2026-10-10, a tokenless 304 still spent one of the 60.
/// </summary>
public interface ICorpusSnapshot
{
    IProjectSource Source { get; }

    /// <summary>The directory listing's ETag, or null when GitHub sent none.</summary>
    string? ETag { get; }

    /// <summary>Every <c>.beh</c> and <c>.bindings.json</c> file in the directory, by file name, in code-unit order.</summary>
    IReadOnlyDictionary<string, ISnapshotFile> Files { get; }
}
