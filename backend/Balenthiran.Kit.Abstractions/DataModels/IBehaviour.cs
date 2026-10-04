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

    bool? ReviewExplicit { get; }
}
