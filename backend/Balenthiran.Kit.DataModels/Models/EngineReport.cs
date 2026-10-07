using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>The engine's verdicts on one resolved corpus, in the shape the project view serves them.</summary>
public sealed class EngineReport : IEngineReport
{
    [JsonPropertyOrder(1)]
    public required AdjudicationReport Adjudication { get; init; }

    [JsonPropertyOrder(2)]
    public required SurfaceReport Surface { get; init; }

    // `object`, not IQuestion: System.Text.Json writes an object-typed element by its
    // RUNTIME type, and the two question shapes have different keys in different orders.
    [JsonPropertyOrder(3)]
    public required List<object> Questions { get; init; }

    [JsonPropertyOrder(4)]
    public required RequiresReport Requires { get; init; }

    IAdjudicationReport IEngineReport.Adjudication => Adjudication;

    ISurfaceReport IEngineReport.Surface => Surface;

    IReadOnlyList<IQuestion> IEngineReport.Questions => Questions.Cast<IQuestion>().ToList();

    IRequiresReport IEngineReport.Requires => Requires;
}
