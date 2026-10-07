using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A question raised because two behaviours provide different values for one symbol. Always a decision.</summary>
public sealed class ConflictQuestion : IQuestion
{
    [JsonPropertyOrder(1)]
    public required string Kind { get; init; }

    [JsonPropertyOrder(2)]
    public required string Tier { get; init; }

    [JsonPropertyOrder(3)]
    public required string Key { get; init; }

    [JsonPropertyOrder(4)]
    public required string Title { get; init; }

    [JsonPropertyOrder(5)]
    public required List<QuestionSide> Sides { get; init; }

    [JsonPropertyOrder(6)]
    public required List<string> Held { get; init; }

    [JsonPropertyOrder(7)]
    public required List<Challenger> Challengers { get; init; }

    [JsonPropertyOrder(8)]
    public required string? Asks { get; init; }

    [JsonPropertyOrder(9)]
    public required List<Option> Options { get; init; }

    [JsonPropertyOrder(10)]
    public required Recommendation? Recommend { get; init; }

    [JsonPropertyOrder(11)]
    public required string? Against { get; init; }

    [JsonPropertyOrder(12)]
    public required string? Owner { get; init; }

    [JsonPropertyOrder(13)]
    public required List<Citation> Cites { get; init; }
}
