using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Kit's feature switches as seen by the current session (BEH-FLAGS-1, BEH-FLAGS-2).</summary>
public sealed class FeatureSwitches : IFeatureSwitches
{
    [JsonPropertyOrder(1)]
    public required string Group { get; init; }

    [JsonPropertyOrder(2)]
    public required IReadOnlyList<string> Off { get; init; }

    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<IFeatureSwitch>? Features { get; init; }
}
