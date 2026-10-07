using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A later <c>provides</c> that disagrees with the value a symbol already holds.</summary>
public sealed class Challenger : IChallenger
{
    [JsonPropertyOrder(1)]
    public required string From { get; init; }

    [JsonPropertyOrder(2)]
    public required List<string> Value { get; init; }

    [JsonPropertyOrder(3)]
    public required string At { get; init; }

    IReadOnlyList<string> IChallenger.Value => Value;
}
