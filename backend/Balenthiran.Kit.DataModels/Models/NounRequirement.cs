using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Everything one noun owes, merged across every step that uses it.</summary>
public sealed class NounRequirement : INounRequirement
{
    [JsonPropertyOrder(1)]
    public required string Noun { get; init; }

    [JsonPropertyOrder(2)]
    public required string Kind { get; init; }

    [JsonPropertyOrder(3)]
    public required string Name { get; init; }

    [JsonPropertyOrder(4)]
    public required List<string> UsedBy { get; init; }

    [JsonPropertyOrder(5)]
    public required bool Bound { get; init; }

    [JsonPropertyOrder(6)]
    public required bool Satisfied { get; init; }

    [JsonPropertyOrder(7)]
    public required List<Need> Needs { get; init; }

    IReadOnlyList<string> INounRequirement.UsedBy => UsedBy;

    IReadOnlyList<INeed> INounRequirement.Needs => Needs;
}
