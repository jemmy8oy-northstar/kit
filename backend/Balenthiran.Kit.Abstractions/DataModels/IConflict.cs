namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// The "supersede?" case: two behaviours provide different values for one symbol.
/// Structural, no LLM involved — the held value never moves, the disagreement is reported.
/// </summary>
public interface IConflict
{
    string Key { get; }

    IReadOnlyList<string> Held { get; }

    IReadOnlyList<string> Holders { get; }

    IReadOnlyList<IChallenger> Challengers { get; }
}
