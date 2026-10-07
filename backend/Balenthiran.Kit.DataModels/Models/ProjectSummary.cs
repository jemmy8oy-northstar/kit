using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One row of the project list: counts, not the projection.</summary>
public sealed class ProjectSummary : IProjectSummary
{
    [JsonPropertyOrder(1)]
    public required string App { get; init; }

    [JsonPropertyOrder(2)]
    public required string Corpus { get; init; }

    [JsonPropertyOrder(3)]
    public required bool NotReal { get; init; }

    [JsonPropertyOrder(4)]
    public required string? DuplicateOf { get; init; }

    [JsonPropertyOrder(5)]
    public required int Behaviours { get; init; }

    [JsonPropertyOrder(6)]
    public required int Conflicts { get; init; }

    [JsonPropertyOrder(7)]
    public required CoverageSummary Coverage { get; init; }

    [JsonPropertyOrder(8)]
    public required int Unreviewed { get; init; }

    ICoverageSummary IProjectSummary.Coverage => Coverage;
}
