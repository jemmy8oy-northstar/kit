using System.Text.Json.Serialization;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One architecture decision as `GET /api/projects/&lt;app&gt;` serves it (BEH-ARCH-1).</summary>
public sealed class DecisionView
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Title { get; init; }

    [JsonPropertyOrder(3)]
    public required string Part { get; init; }

    [JsonPropertyOrder(4)]
    public required string Chosen { get; init; }

    [JsonPropertyOrder(5)]
    public required List<string> Considered { get; init; }

    [JsonPropertyOrder(6)]
    public required List<string> Because { get; init; }

    [JsonPropertyOrder(7)]
    public required Review Review { get; init; }

    [JsonPropertyOrder(8)]
    public required Source Source { get; init; }

    [JsonPropertyOrder(9)]
    public required string At { get; init; }
}
