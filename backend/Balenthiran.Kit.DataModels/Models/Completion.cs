using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="ICompletion"/>
public sealed class Completion : ICompletion
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string? At { get; init; }
}
