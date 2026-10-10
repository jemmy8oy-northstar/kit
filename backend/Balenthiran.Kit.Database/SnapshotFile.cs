using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <summary><see cref="ISnapshotFile"/>, as <see cref="GitHubCorpusReader"/> reads it.</summary>
public sealed class SnapshotFile : ISnapshotFile
{
    public required string Sha { get; init; }

    public required string Text { get; init; }
}
