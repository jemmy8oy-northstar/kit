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

    // kit#237. Last, so every key already served keeps its place.
    [JsonPropertyOrder(9)]
    public required string Project { get; init; }

    // kit#284 slice 4, after `project` for the same reason; true or absent.
    [JsonPropertyOrder(10)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Idea { get; init; }

    bool IProjectSummary.Idea => Idea == true;

    // BEH-SETTINGS-4: "self" when this project's primary file carries `# kit:promote self`; absent otherwise.
    [JsonPropertyOrder(11)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Promote { get; init; }

    string? IProjectSummary.Promote => Promote;

    ICoverageSummary IProjectSummary.Coverage => Coverage;
}
