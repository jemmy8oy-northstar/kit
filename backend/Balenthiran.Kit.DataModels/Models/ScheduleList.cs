using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IScheduleList"/>
public sealed class ScheduleList : IScheduleList
{
    [JsonPropertyOrder(1)]
    public required string App { get; init; }

    [JsonPropertyOrder(2)]
    public required string File { get; init; }

    [JsonPropertyOrder(3)]
    public required List<ScheduleEntry> Scheduled { get; init; }

    [JsonPropertyOrder(4)]
    public required List<ScheduleEntry> Backlog { get; init; }

    IReadOnlyList<IScheduleEntry> IScheduleList.Scheduled => Scheduled;

    IReadOnlyList<IScheduleEntry> IScheduleList.Backlog => Backlog;
}
