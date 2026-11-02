using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="ITriageStatus"/>
public sealed class TriageStatus : ITriageStatus
{
    [JsonPropertyOrder(1)]
    public required bool Configured { get; init; }

    [JsonPropertyOrder(2)]
    public string? Model { get; init; }

    [JsonPropertyOrder(3)]
    public int? Remaining { get; init; }
}
