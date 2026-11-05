using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IAgentRunStarted"/>
public sealed class AgentRunStarted : IAgentRunStarted
{
    [JsonPropertyOrder(1)]
    public required IAgentRun Run { get; init; }

    [JsonPropertyOrder(2)]
    public required string Token { get; init; }

    [JsonPropertyOrder(3)]
    public required string Launch { get; init; }
}
