using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Coverage that was measured, as ids (BEH-HEALTH-1): the shape <c>DetailCoverage</c> in the UI's types reads.</summary>
public sealed class AvailableCoverage : IAvailableCoverage
{
    [JsonPropertyOrder(1)]
    public required bool Available { get; init; }

    [JsonPropertyOrder(2)]
    public required List<string> Covered { get; init; }

    [JsonPropertyOrder(3)]
    public required List<string> Uncovered { get; init; }

    IReadOnlyList<string> IAvailableCoverage.Covered => Covered;

    IReadOnlyList<string> IAvailableCoverage.Uncovered => Uncovered;
}
