using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IPushSubscription"/>
public sealed class PushSubscription : IPushSubscription
{
    [JsonPropertyOrder(1)]
    public required string Endpoint { get; init; }

    [JsonPropertyOrder(2)]
    public required string P256dh { get; init; }

    [JsonPropertyOrder(3)]
    public required string Auth { get; init; }
}
