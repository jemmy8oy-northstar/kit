using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>What a noun owes, plus its binding and the other corpora that name it.</summary>
public sealed class NounView : INounView
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

    [JsonPropertyOrder(8)]
    public required JsonNode? Binding { get; init; }

    [JsonPropertyOrder(9)]
    public required List<string> SharedWith { get; init; }

    IReadOnlyList<string> INounView.UsedBy => UsedBy;

    IReadOnlyList<INeed> INounView.Needs => Needs;

    IReadOnlyList<string> INounView.SharedWith => SharedWith;
}
