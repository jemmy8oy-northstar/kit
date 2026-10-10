using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <summary><see cref="ICorpusSnapshot"/>, as <see cref="GitHubCorpusReader"/> reads it.</summary>
public sealed class CorpusSnapshot : ICorpusSnapshot
{
    public required IProjectSource Source { get; init; }

    public string? ETag { get; init; }

    public required IReadOnlyDictionary<string, ISnapshotFile> Files { get; init; }
}
