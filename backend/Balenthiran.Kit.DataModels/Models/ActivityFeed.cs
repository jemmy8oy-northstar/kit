using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IActivityFeed"/>
public sealed class ActivityFeed : IActivityFeed
{
    [JsonPropertyOrder(1)]
    public required List<ActivityEntry> Entries { get; init; }

    [JsonPropertyOrder(2)]
    public required List<ActivityGap> NoHistory { get; init; }

    IReadOnlyList<IActivityEntry> IActivityFeed.Entries => Entries;

    IReadOnlyList<IActivityGap> IActivityFeed.NoHistory => NoHistory;
}
