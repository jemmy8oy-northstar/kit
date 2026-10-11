using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One feature as the project view serves it (kit#237).</summary>
public sealed class FeatureView : IFeatureView
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Title { get; init; }

    [JsonPropertyOrder(3)]
    public required List<string> In { get; init; }

    [JsonPropertyOrder(4)]
    public required string At { get; init; }

    IReadOnlyList<string> IFeatureView.In => In;
}
