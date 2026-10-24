using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="INotificationFeed"/>
public sealed class NotificationFeed : INotificationFeed
{
    [JsonPropertyOrder(1)]
    public required List<Notification> Entries { get; init; }

    [JsonPropertyOrder(2)]
    public int Skipped { get; init; }

    [JsonPropertyOrder(3)]
    public string? Reason { get; init; }

    IReadOnlyList<INotification> INotificationFeed.Entries => Entries;
}
