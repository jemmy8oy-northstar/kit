using System.Text.Json.Serialization;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One possibly-broken test link (BEH-HEALTH-4).</summary>
public sealed class BrokenLink
{
    [JsonPropertyOrder(1)]
    public required string BehaviourId { get; init; }

    [JsonPropertyOrder(2)]
    public required string Title { get; init; }

    [JsonPropertyOrder(3)]
    public required string Reason { get; init; }
}
