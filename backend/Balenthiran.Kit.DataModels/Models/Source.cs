using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Whether a human wrote this behaviour or something inferred it, and from what.</summary>
public sealed class Source : ISource
{
    [JsonPropertyOrder(1)]
    public required string Origin { get; init; }

    [JsonPropertyOrder(2)]
    public required string? Ref { get; init; }
}
