namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One behaviour: the unit a corpus is a list of.</summary>
public interface IBehaviour
{
    string Id { get; }

    string Title { get; }

    string? Actor { get; }

    IReadOnlyList<IStep> Steps { get; }

    IReadOnlyList<IUnknown> Unknowns { get; }

    IReadOnlyList<IProvide> Provides { get; }

    IReadOnlyList<IIdRef> Serves { get; }

    string At { get; }

    string? Asks { get; }

    IReadOnlyList<IOption> Options { get; }

    IRecommendation? Recommend { get; }

    string? Against { get; }

    IReadOnlyList<IIdRef> Cites { get; }

    ISource Source { get; }

    IReview Review { get; }

    /// <summary>Spec'd on <c>dev</c> ahead of its code (kit#155); a <c>pending</c> line.</summary>
    bool Pending { get; }

    /// <summary>The layer whose tests prove it (kit#89): <c>ux</c>, <c>technical</c> or <c>ui</c>.</summary>
    string Layer { get; }

    bool? ReviewExplicit { get; }

    /// <summary>Holes another behaviour filled. Null until <c>resolve</c> has run.</summary>
    IReadOnlyList<IFilled>? Filled { get; }

    /// <summary>Holes nothing filled. Null until <c>resolve</c> has run.</summary>
    IReadOnlyList<IOpenHole>? Open { get; }
}
