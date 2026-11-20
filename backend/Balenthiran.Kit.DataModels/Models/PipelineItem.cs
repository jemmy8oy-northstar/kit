using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IPipelineItem"/>
public sealed class PipelineItem : IPipelineItem
{
    [JsonPropertyOrder(1)]
    public required string Project { get; init; }

    [JsonPropertyOrder(2)]
    public required string Slug { get; init; }

    [JsonPropertyOrder(3)]
    public required string Stage { get; init; }

    [JsonPropertyOrder(4)]
    public required string Status { get; init; }

    [JsonPropertyOrder(5)]
    public string? StoppedBecause { get; init; }

    [JsonPropertyOrder(6)]
    public string? PullRequest { get; init; }

    [JsonPropertyOrder(7)]
    public string? Flag { get; init; }

    [JsonPropertyOrder(8)]
    public string? Review { get; init; }

    [JsonPropertyOrder(9)]
    public string? ReviewNote { get; init; }

    [JsonPropertyOrder(10)]
    public bool Merged { get; init; }

    [JsonPropertyOrder(11)]
    public string? MergeNote { get; init; }
}
