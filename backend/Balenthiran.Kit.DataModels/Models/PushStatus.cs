using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IPushStatus"/>
public sealed class PushStatus : IPushStatus
{
    [JsonPropertyOrder(1)]
    public required bool Configured { get; init; }

    [JsonPropertyOrder(2)]
    public string? PublicKey { get; init; }
}
