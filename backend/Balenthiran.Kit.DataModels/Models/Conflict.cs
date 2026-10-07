using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Two behaviours provide different values for one symbol.</summary>
public sealed class Conflict : IConflict
{
    [JsonPropertyOrder(1)]
    public required string Key { get; init; }

    [JsonPropertyOrder(2)]
    public required List<string> Held { get; init; }

    [JsonPropertyOrder(3)]
    public required List<string> Holders { get; init; }

    [JsonPropertyOrder(4)]
    public required List<Challenger> Challengers { get; init; }

    IReadOnlyList<string> IConflict.Held => Held;

    IReadOnlyList<string> IConflict.Holders => Holders;

    IReadOnlyList<IChallenger> IConflict.Challengers => Challengers;
}
