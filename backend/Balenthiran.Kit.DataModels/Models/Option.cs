using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A choice AND what changes if it is taken. The consequence is not decoration.</summary>
public sealed class Option : IOption
{
    [JsonPropertyOrder(1)]
    public required string Label { get; init; }

    [JsonPropertyOrder(2)]
    public required string Consequence { get; init; }

    [JsonPropertyOrder(3)]
    public required string At { get; init; }
}
