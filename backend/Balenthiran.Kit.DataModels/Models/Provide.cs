using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Where an inference is written down, so an approve/deny has something to point at.</summary>
public sealed class Provide : IProvide
{
    [JsonPropertyOrder(1)]
    public required string Kind { get; init; }

    [JsonPropertyOrder(2)]
    public required string Name { get; init; }

    [JsonPropertyOrder(3)]
    public required string Slot { get; init; }

    [JsonPropertyOrder(4)]
    public required List<string> Value { get; init; }

    [JsonPropertyOrder(5)]
    public required string From { get; init; }

    [JsonPropertyOrder(6)]
    public required string At { get; init; }

    // The interface view. Explicit, so System.Text.Json — which serialises
    // public members only — never sees it, and the wire shape stays the
    // concrete properties above with their stated order.
    IReadOnlyList<string> IProvide.Value => Value;
}
