using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IActivityEntry"/>
public sealed class ActivityEntry : IActivityEntry
{
    [JsonPropertyOrder(1)]
    public required string Commit { get; init; }

    [JsonPropertyOrder(2)]
    public required string At { get; init; }

    [JsonPropertyOrder(3)]
    public required string App { get; init; }

    [JsonPropertyOrder(4)]
    public required string Summary { get; init; }
}
