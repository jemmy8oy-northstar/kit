using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IAgentRun"/>
public sealed class AgentRun : IAgentRun
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Owner { get; init; }

    [JsonPropertyOrder(3)]
    public required string Project { get; init; }

    [JsonPropertyOrder(4)]
    public required string Model { get; init; }

    [JsonPropertyOrder(5)]
    public required string Status { get; init; }

    [JsonPropertyOrder(6)]
    public required DateTimeOffset Started { get; init; }

    [JsonPropertyOrder(7)]
    public DateTimeOffset? Ended { get; init; }

    [JsonPropertyOrder(8)]
    public long InputTokens { get; init; }

    [JsonPropertyOrder(9)]
    public long OutputTokens { get; init; }

    [JsonPropertyOrder(10)]
    public long CacheWriteTokens { get; init; }

    [JsonPropertyOrder(11)]
    public long CacheReadTokens { get; init; }

    [JsonPropertyOrder(12)]
    public decimal CostUsd { get; init; }

    [JsonPropertyOrder(13)]
    public string? Failure { get; init; }

    [JsonPropertyOrder(14)]
    public string? WorkItem { get; init; }

    [JsonPropertyOrder(15)]
    public string? Stage { get; init; }
}
