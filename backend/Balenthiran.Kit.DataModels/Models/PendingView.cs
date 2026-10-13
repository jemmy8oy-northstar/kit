using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IPendingView"/>
public sealed class PendingView : IPendingView
{
    [JsonPropertyOrder(1)]
    public required string App { get; init; }

    [JsonPropertyOrder(2)]
    public string? Head { get; init; }

    [JsonPropertyOrder(3)]
    public required string Base { get; init; }

    [JsonPropertyOrder(4)]
    public IReadOnlyList<IPendingEdit> Edits { get; init; } = [];

    [JsonPropertyOrder(5)]
    public int? PullNumber { get; init; }

    [JsonPropertyOrder(6)]
    public string? PullUrl { get; init; }

    [JsonPropertyOrder(7)]
    public string? Reason { get; init; }
}
