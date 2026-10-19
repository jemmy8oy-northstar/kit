using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>An <c>epic</c> block (BEH-EPIC-2): an id and a title. The project view serves it as is.</summary>
public sealed class Epic : IEpic
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Title { get; init; }

    [JsonPropertyOrder(3)]
    public required string At { get; init; }
}
