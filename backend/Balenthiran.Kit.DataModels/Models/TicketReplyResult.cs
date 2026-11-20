using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="ITicketReplyResult"/>
public sealed class TicketReplyResult : ITicketReplyResult
{
    [JsonPropertyOrder(1)]
    public required bool Woke { get; init; }
}
