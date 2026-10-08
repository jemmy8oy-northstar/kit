using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A recommendation and why. One per behaviour, or none.</summary>
public sealed class Recommendation : IRecommendation
{
    [JsonPropertyOrder(1)]
    public required string Label { get; init; }

    [JsonPropertyOrder(2)]
    public required string Why { get; init; }

    [JsonPropertyOrder(3)]
    public required string At { get; init; }
}
