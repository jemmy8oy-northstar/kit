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

    // Absent once measured (BEH-HEALTH-1): an available row has no reason, and `reason: null` is not in the UI's type.
    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required string? Reason { get; init; }

    // BEH-HEALTH-2: absent, not null, when the mapping names no (well-formed) last PR.
    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastImprovedBy? LastImprovedBy { get; init; }

    ILastImprovedBy? ICoverageSummary.LastImprovedBy => LastImprovedBy;
}
