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

    IReadOnlyList<IIdRef> IFeature.In => In;
}
