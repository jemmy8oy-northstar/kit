using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A hole another behaviour's <c>provides</c> filled.</summary>
public sealed class Filled : IFilled
{
    [JsonPropertyOrder(1)]
    public required string Key { get; init; }

    [JsonPropertyOrder(2)]
    public required List<string> Value { get; init; }

    [JsonPropertyOrder(3)]
    public required List<string> From { get; init; }

    [JsonPropertyOrder(4)]
    public required string At { get; init; }

    IReadOnlyList<string> IFilled.Value => Value;

    IReadOnlyList<string> IFilled.From => From;
}
