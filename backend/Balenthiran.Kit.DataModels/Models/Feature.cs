using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A <c>feature</c> block (kit#237): an id, a title, and the parents it is tagged into.</summary>
public sealed class Feature : IFeature
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Title { get; init; }

    [JsonPropertyOrder(3)]
    public List<IdRef> In { get; } = [];

    [JsonPropertyOrder(4)]
    public required string At { get; init; }

    /// <summary>Where it stands on the stages board (FEAT-STAGES), or null when it is not on it.</summary>
    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Stage { get; set; }

    /// <summary>The epics it is in (BEH-EPIC-2), or null when none, so the shape predating epics holds.</summary>
    [JsonPropertyOrder(6)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<IdRef>? Epics { get; set; }

    IReadOnlyList<IIdRef> IFeature.In => In;

    IReadOnlyList<IIdRef> IFeature.Epics => (IReadOnlyList<IIdRef>?)Epics ?? [];
}
