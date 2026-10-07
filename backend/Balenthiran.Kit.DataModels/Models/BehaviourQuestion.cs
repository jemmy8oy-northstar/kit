using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A question about one unreviewed inference: a decision when nothing documented displays it or a human asked, else a review.</summary>
public sealed class BehaviourQuestion : IQuestion
{
    [JsonPropertyOrder(1)]
    public required string Kind { get; init; }

    [JsonPropertyOrder(2)]
    public required string Tier { get; init; }

    [JsonPropertyOrder(3)]
    public required string Key { get; init; }

    [JsonPropertyOrder(4)]
    public required string Id { get; init; }

    [JsonPropertyOrder(5)]
    public required string Title { get; init; }

    [JsonPropertyOrder(6)]
    public required Source Source { get; init; }

    [JsonPropertyOrder(7)]
    public required List<string> Serves { get; init; }

    [JsonPropertyOrder(8)]
    public required List<string> Contracts { get; init; }

    [JsonPropertyOrder(9)]
    public required string? Asks { get; init; }

    [JsonPropertyOrder(10)]
    public required List<Option> Options { get; init; }

    [JsonPropertyOrder(11)]
    public required Recommendation? Recommend { get; init; }

    [JsonPropertyOrder(12)]
    public required string? Against { get; init; }

    [JsonPropertyOrder(13)]
    public required List<Citation> Cites { get; init; }
}
