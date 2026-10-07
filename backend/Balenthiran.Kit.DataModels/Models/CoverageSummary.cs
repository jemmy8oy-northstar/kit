using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Coverage on a list row. `covered` is null — not 0 — when there was nothing to read.</summary>
public sealed class CoverageSummary : ICoverageSummary
{
    [JsonPropertyOrder(1)]
    public required bool Available { get; init; }

    [JsonPropertyOrder(2)]
    public required int? Covered { get; init; }

    [JsonPropertyOrder(3)]
    public required int? Uncovered { get; init; }

    [JsonPropertyOrder(4)]
    public required string? Reason { get; init; }
}
