using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="ISessionCost"/>
public sealed class SessionCost : ISessionCost
{
    [JsonPropertyOrder(1)]
    public required string RunId { get; init; }

    [JsonPropertyOrder(2)]
    public required DateTimeOffset Date { get; init; }

    [JsonPropertyOrder(3)]
    public required string Project { get; init; }

    [JsonPropertyOrder(4)]
    public required string Model { get; init; }

    [JsonPropertyOrder(5)]
    public required decimal CostUsd { get; init; }
}
