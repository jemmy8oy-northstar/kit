using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IScheduleEntry"/>
public sealed class ScheduleEntry : IScheduleEntry
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public string? Title { get; init; }

    [JsonPropertyOrder(3)]
    public required string State { get; init; }

    [JsonPropertyOrder(4)]
    public bool Next { get; init; }
}
