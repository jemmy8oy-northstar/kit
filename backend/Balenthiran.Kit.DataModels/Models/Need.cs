using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One obligation a noun owes, the verbs that impose it, and whether the current binding meets it.</summary>
public sealed class Need : INeed
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Surface { get; init; }

    [JsonPropertyOrder(3)]
    public required List<string> Verbs { get; init; }

    [JsonPropertyOrder(4)]
    public required bool Met { get; init; }

    IReadOnlyList<string> INeed.Verbs => Verbs;
}
