using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One behaviour a question cites as evidence. A cited id that is not in the corpus keeps its id as its title.</summary>
public sealed class Citation : ICitation
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Title { get; init; }

    [JsonPropertyOrder(3)]
    public required string? Ref { get; init; }

    [JsonPropertyOrder(4)]
    public required List<string> Contracts { get; init; }

    IReadOnlyList<string> ICitation.Contracts => Contracts;
}
