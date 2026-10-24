using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IAgentSchedule"/>
public sealed class AgentSchedule : IAgentSchedule
{
    [JsonPropertyOrder(1)]
    public bool Suspended { get; init; }

    [JsonPropertyOrder(2)]
    public string? Until { get; init; }

    [JsonPropertyOrder(3)]
    public bool ByAgent { get; init; }

    [JsonPropertyOrder(4)]
    public string? Why { get; init; }

    [JsonPropertyOrder(5)]
    public string? Reason { get; init; }
}
