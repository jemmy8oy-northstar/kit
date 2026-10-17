namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Everything the behaviour page reads about one corpus — the body of `GET /api/projects/<app>`.</summary>
public interface IProjectView
{
    string App { get; }

    string Corpus { get; }

    bool NotReal { get; }

    /// <summary>A <c># kit:idea</c> file (kit#284): app ideas that are not projects yet, kept off the board.</summary>
    bool Idea { get; }

    /// <summary>Its <c># kit:principle</c> lines (kit#284), in file order; empty when it declares none.</summary>
    IReadOnlyList<IPrincipleView> Principles { get; }

    string? DuplicateOf { get; }

    IReadOnlyList<IBehaviourView> Behaviours { get; }

    IReadOnlyList<IConflict> Conflicts { get; }

    IReadOnlyList<IGeneratedView> Generated { get; }

    IUnavailableCoverage Coverage { get; }

    IAdjudicationReport Adjudication { get; }

    ISurfaceReport Surface { get; }

    IReadOnlyList<IQuestion> Questions { get; }

    IRequiresView Requires { get; }

    /// <summary>The corpus's features (kit#237); empty for a corpus that declares none.</summary>
    IReadOnlyList<IFeatureView> Features { get; }

    /// <summary>See <see cref="IProjectSummary.Project"/>.</summary>
    string Project { get; }
}
