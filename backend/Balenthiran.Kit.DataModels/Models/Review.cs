using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Whether anyone has ruled on this behaviour, and what they said.</summary>
public sealed class Review : IReview
{
    [JsonPropertyOrder(1)]
    public required string State { get; init; }

    [JsonPropertyOrder(2)]
    public required string? Note { get; init; }
}
