using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="ITicketView"/>
public sealed class TicketView : ITicketView
{
    [JsonPropertyOrder(1)]
    public required string Slug { get; init; }

    [JsonPropertyOrder(2)]
    public required string Title { get; init; }

    [JsonPropertyOrder(3)]
    public required string State { get; init; }

    [JsonPropertyOrder(4)]
    public required int Priority { get; init; }

    [JsonPropertyOrder(5)]
    public required IReadOnlyList<string> Cards { get; init; }

    [JsonPropertyOrder(6)]
    public string? PullRequest { get; init; }

    [JsonPropertyOrder(7)]
    public string? BlockedBecause { get; init; }

    [JsonPropertyOrder(8)]
    public required IReadOnlyList<ITicketCommentView> Comments { get; init; }
}
