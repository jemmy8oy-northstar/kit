using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="ICorpusVersion"/>
public sealed class CorpusVersion : ICorpusVersion
{
    public required DateTimeOffset At { get; init; }

    public required IReadOnlyList<KeyValuePair<string, bool>> Pending { get; init; }
}
