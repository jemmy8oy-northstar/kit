using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IFinishedRun"/>
public sealed class FinishedRun : IFinishedRun
{
    [JsonPropertyOrder(1)]
    public required string Project { get; init; }

    [JsonPropertyOrder(2)]
    public required string WorkItem { get; init; }

    [JsonPropertyOrder(3)]
    public required IReadOnlyList<IFinishedStage> Stages { get; init; }

    [JsonPropertyOrder(4)]
    public required IReadOnlyList<IRequestedReview> Reviews { get; init; }
}
