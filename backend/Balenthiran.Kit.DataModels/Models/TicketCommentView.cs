using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="ITicketCommentView"/>
public sealed class TicketCommentView : ITicketCommentView
{
    [JsonPropertyOrder(1)]
    public required string Author { get; init; }

    [JsonPropertyOrder(2)]
    public required string Body { get; init; }

    [JsonPropertyOrder(3)]
    public required string At { get; init; }
}
