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
}
