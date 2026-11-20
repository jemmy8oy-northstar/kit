using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IAgentRunList"/>
public sealed class AgentRunList : IAgentRunList
{
    [JsonPropertyOrder(1)]
    public required decimal CapUsd { get; init; }

    [JsonPropertyOrder(2)]
    public required IReadOnlyList<IAgentRun> Runs { get; init; }

    [JsonPropertyOrder(3)]
    public IReadOnlyList<IRunClaim> Claims { get; init; } = [];
}
