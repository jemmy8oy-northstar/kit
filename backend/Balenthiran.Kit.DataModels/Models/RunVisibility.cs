using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IRunVisibility"/>
public sealed class RunVisibility : IRunVisibility
{
    [JsonPropertyOrder(1)]
    public required IReadOnlyList<IRunningAgent> Running { get; init; }

    [JsonPropertyOrder(2)]
    public required IReadOnlyList<IFinishedRun> Finished { get; init; }

    [JsonPropertyOrder(3)]
    public required IReadOnlyList<IRecentRun> Recent { get; init; }
}
