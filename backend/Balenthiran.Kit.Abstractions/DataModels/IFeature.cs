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
}
