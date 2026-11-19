using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IRunClaim"/>
public sealed class RunClaim : IRunClaim
{
    [JsonPropertyOrder(1)]
    public required string RunId { get; init; }

    [JsonPropertyOrder(2)]
    public required DateTimeOffset ClaimedAt { get; init; }

    [JsonPropertyOrder(3)]
    public required DateTimeOffset LostAt { get; init; }
}
