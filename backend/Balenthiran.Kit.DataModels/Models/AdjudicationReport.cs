using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>How many behaviours are defined vs inferred, and the ids in each review state.</summary>
public sealed class AdjudicationReport : IAdjudicationReport
{
    [JsonPropertyOrder(1)]
    public required int Defined { get; init; }

    [JsonPropertyOrder(2)]
    public required int Inferred { get; init; }

    [JsonPropertyOrder(3)]
    public required List<string> Unreviewed { get; init; }

    [JsonPropertyOrder(4)]
    public required List<string> Approved { get; init; }

    [JsonPropertyOrder(5)]
    public required List<string> Denied { get; init; }

    [JsonPropertyOrder(6)]
    public required List<string> Untraceable { get; init; }

    IReadOnlyList<string> IAdjudicationReport.Unreviewed => Unreviewed;

    IReadOnlyList<string> IAdjudicationReport.Approved => Approved;

    IReadOnlyList<string> IAdjudicationReport.Denied => Denied;

    IReadOnlyList<string> IAdjudicationReport.Untraceable => Untraceable;
}
