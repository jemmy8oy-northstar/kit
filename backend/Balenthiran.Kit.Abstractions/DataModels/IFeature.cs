namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// A <c>feature</c> block (kit#237): a page or a part of one, which other features and
/// behaviours are tagged into with <c>in</c>. It holds nothing but its parents.
/// </summary>
public interface IFeature
{
    string Id { get; }

    string Title { get; }

    /// <summary>Its parents, one per <c>in</c> line. A tag, not an owner: there can be several.</summary>
    IReadOnlyList<IIdRef> In { get; }

    string At { get; }

    /// <summary>
    /// Its <c>stage</c> line (FEAT-STAGES): <c>problem</c>, <c>ux</c>, <c>logic</c>, <c>architecture</c>,
    /// <c>development</c> or <c>ui</c>. Null when it has none, which keeps it off the stages board.
    /// </summary>
    string? Stage { get; }

    /// <summary>
    /// The epics it is in (BEH-EPIC-2), one per <c>in EPIC-…</c> line. Kept apart from
    /// <see cref="In"/> so the project's navigation is unchanged by an epic.
    /// </summary>
    IReadOnlyList<IIdRef> Epics { get; }
}
