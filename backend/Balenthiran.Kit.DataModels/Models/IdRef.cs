using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A reference to another behaviour by id — what `serves` and `cites` both are.</summary>
public sealed class IdRef : IIdRef
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string At { get; init; }
}
