using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IPipelineItemList"/>
public sealed class PipelineItemList : IPipelineItemList
{
    [JsonPropertyOrder(1)]
    public required bool On { get; init; }

    [JsonPropertyOrder(2)]
    public required IReadOnlyList<IPipelineItem> Items { get; init; }

    [JsonPropertyOrder(3)]
    public string? Paused { get; init; }
}
