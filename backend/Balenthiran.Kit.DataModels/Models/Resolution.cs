using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>What stage 2 returns.</summary>
public sealed class Resolution : IResolution
{
    public required List<Behaviour> Behaviours { get; init; }

    /// <summary>First-provided order is output, so this is ordered rather than a plain dictionary.</summary>
    public required OrderedDictionary<string, Symbol> Symbols { get; init; }

    public required List<Conflict> Conflicts { get; init; }

    IReadOnlyList<IBehaviour> IResolution.Behaviours => Behaviours;

    IReadOnlyList<KeyValuePair<string, ISymbol>> IResolution.Symbols =>
        Symbols.Select(kv => new KeyValuePair<string, ISymbol>(kv.Key, kv.Value)).ToList();

    IReadOnlyList<IConflict> IResolution.Conflicts => Conflicts;
}
