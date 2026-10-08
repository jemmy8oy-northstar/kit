namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A step: a verb plus noun references. `?slot` marks a hole.</summary>
public interface IStep
{
    string Kind { get; }

    string Verb { get; }

    string Text { get; }

    IReadOnlyList<IReference> Refs { get; }

    IReadOnlyList<IHole> Holes { get; }

    string At { get; }

    /// <summary>Slot → value, set by <c>resolve</c> for each hole it filled; null until then.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Resolved { get; }
}
