using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="INotification"/>
public sealed class Notification : INotification
{
    [JsonPropertyOrder(1)]
    public required string At { get; init; }

    [JsonPropertyOrder(2)]
    public required string Kind { get; init; }

    [JsonPropertyOrder(3)]
    public required string Text { get; init; }
}
