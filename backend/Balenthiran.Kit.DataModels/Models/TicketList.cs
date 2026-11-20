using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="ITicketList"/>
public sealed class TicketList : ITicketList
{
    [JsonPropertyOrder(1)]
    public required bool On { get; init; }

    [JsonPropertyOrder(2)]
    public required IReadOnlyList<ITicketView> Tickets { get; init; }
}
