using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IFinishedStage"/>
public sealed class FinishedStage : IFinishedStage
{
    [JsonPropertyOrder(1)]
    public required string RunId { get; init; }

    [JsonPropertyOrder(2)]
    public string? Stage { get; init; }

    [JsonPropertyOrder(3)]
    public required string Model { get; init; }

    [JsonPropertyOrder(4)]
    public decimal? CostUsd { get; init; }
}
