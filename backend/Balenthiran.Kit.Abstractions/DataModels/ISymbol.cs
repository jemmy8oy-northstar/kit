namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// One entry in the symbol table <c>resolve</c> builds from every <c>provides</c>:
/// the value the FIRST provider asserted, everyone who agreed, and anyone who did not.
/// </summary>
public interface ISymbol
{
    IReadOnlyList<string> Value { get; }

    IReadOnlyList<string> Contributors { get; }

    string At { get; }

    /// <summary>Null, not empty, when nobody disagreed — the key is absent in the engine's output.</summary>
    IReadOnlyList<IChallenger>? Conflict { get; }
}
