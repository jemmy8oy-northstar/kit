using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="INote"/>
public sealed class Note : INote
{
    [JsonPropertyOrder(1)]
    public required string At { get; init; }

    [JsonPropertyOrder(2)]
    public string? Behaviour { get; init; }

    [JsonPropertyOrder(3)]
    public required string Text { get; init; }
}
