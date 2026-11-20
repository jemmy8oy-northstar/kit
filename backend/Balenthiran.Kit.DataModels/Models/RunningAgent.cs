using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IRunningAgent"/>
public sealed class RunningAgent : IRunningAgent
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Project { get; init; }

    [JsonPropertyOrder(3)]
    public string? WorkItem { get; init; }

    [JsonPropertyOrder(4)]
    public string? Stage { get; init; }

    [JsonPropertyOrder(5)]
    public required string Model { get; init; }

    [JsonPropertyOrder(6)]
    public required DateTimeOffset Started { get; init; }

    [JsonPropertyOrder(7)]
    public string? Flag { get; init; }

    [JsonPropertyOrder(8)]
    public string? FlaggedBy { get; init; }
}
