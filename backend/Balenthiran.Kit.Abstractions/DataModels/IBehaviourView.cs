namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One behaviour as the project view serves it.</summary>
public interface IBehaviourView
{
    string Id { get; }

    string Title { get; }

    string? Actor { get; }

    IReadOnlyList<IStepView> Steps { get; }

    IReadOnlyList<string> Open { get; }

    IReadOnlyList<IFilledView> Filled { get; }

    ISource Source { get; }

    IReview Review { get; }

    string? Asks { get; }

    string At { get; }

    bool Pending { get; }

    string Layer { get; }

    /// <summary>Parent feature ids (kit#237); empty when untagged.</summary>
    IReadOnlyList<string> In { get; }

    /// <summary>1 (first) to 5, from a <c>priority</c> line (kit#284); null when there is none.</summary>
    int? Priority { get; }
}
