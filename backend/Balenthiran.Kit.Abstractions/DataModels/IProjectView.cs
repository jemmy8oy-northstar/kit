namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Everything the behaviour page reads about one corpus — the body of `GET /api/projects/<app>`.</summary>
public interface IProjectView
{
    string App { get; }

    string Corpus { get; }

    bool NotReal { get; }

    string? DuplicateOf { get; }

    IReadOnlyList<IBehaviourView> Behaviours { get; }

    IReadOnlyList<IConflict> Conflicts { get; }

    IReadOnlyList<IGeneratedView> Generated { get; }

    IUnavailableCoverage Coverage { get; }

    IAdjudicationReport Adjudication { get; }

    ISurfaceReport Surface { get; }

    IReadOnlyList<IQuestion> Questions { get; }

    IRequiresView Requires { get; }
}
