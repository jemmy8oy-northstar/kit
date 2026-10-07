namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The engine's verdicts on one resolved corpus, in the shape the project view serves them.</summary>
public interface IEngineReport
{
    IAdjudicationReport Adjudication { get; }

    ISurfaceReport Surface { get; }

    IReadOnlyList<IQuestion> Questions { get; }

    IRequiresReport Requires { get; }
}
