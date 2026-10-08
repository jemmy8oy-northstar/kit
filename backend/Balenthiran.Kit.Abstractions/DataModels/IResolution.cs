namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>What stage 2 returns: the behaviours with their holes accounted for, the symbol table, and its conflicts.</summary>
public interface IResolution
{
    IReadOnlyList<IBehaviour> Behaviours { get; }

    /// <summary>In first-provided order. Ordered pairs, not a dictionary, because the order is output.</summary>
    IReadOnlyList<KeyValuePair<string, ISymbol>> Symbols { get; }

    IReadOnlyList<IConflict> Conflicts { get; }
}
