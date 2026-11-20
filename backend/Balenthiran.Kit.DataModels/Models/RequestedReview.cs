using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IRequestedReview"/>
public sealed class RequestedReview : IRequestedReview
{
    [JsonPropertyOrder(1)]
    public required string RunId { get; init; }

    [JsonPropertyOrder(2)]
    public string? Stage { get; init; }

    [JsonPropertyOrder(3)]
    public string? RequestedBy { get; init; }
}
