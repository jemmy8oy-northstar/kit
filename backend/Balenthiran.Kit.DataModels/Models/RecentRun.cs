using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IRecentRun"/>
public sealed class RecentRun : IRecentRun
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
    public required string Status { get; init; }

    [JsonPropertyOrder(7)]
    public required DateTimeOffset Started { get; init; }

    [JsonPropertyOrder(8)]
    public DateTimeOffset? Ended { get; init; }

    [JsonPropertyOrder(9)]
    public long Tokens { get; init; }

    [JsonPropertyOrder(10)]
    public string? Failure { get; init; }

    [JsonPropertyOrder(11)]
    public decimal? CostUsd { get; init; }
}
