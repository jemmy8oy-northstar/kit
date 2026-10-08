using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One entry in <c>resolve</c>'s symbol table. Key order measured from the goldens: value,contributors,at[,conflict].</summary>
public sealed class Symbol : ISymbol
{
    [JsonPropertyOrder(1)]
    public required List<string> Value { get; init; }

    [JsonPropertyOrder(2)]
    public required List<string> Contributors { get; init; }

    [JsonPropertyOrder(3)]
    public required string At { get; init; }

    // ABSENT, not null, until a challenger arrives: `kit.js` creates the key lazily,
    // so 17 of the goldens' 18 symbols have no `conflict` key at all.
    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Challenger>? Conflict { get; set; }

    IReadOnlyList<string> ISymbol.Value => Value;

    IReadOnlyList<string> ISymbol.Contributors => Contributors;

    IReadOnlyList<IChallenger>? ISymbol.Conflict => Conflict;
}
