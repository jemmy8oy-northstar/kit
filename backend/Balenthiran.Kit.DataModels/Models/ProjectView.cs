using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Everything the behaviour page reads about one corpus — the body of `GET /api/projects/<app>`.</summary>
public sealed class ProjectView : IProjectView
{
    [JsonPropertyOrder(1)]
    public required string App { get; init; }

    [JsonPropertyOrder(2)]
    public required string Corpus { get; init; }

    [JsonPropertyOrder(3)]
    public required bool NotReal { get; init; }

    [JsonPropertyOrder(4)]
    public required string? DuplicateOf { get; init; }

    [JsonPropertyOrder(5)]
    public required List<BehaviourView> Behaviours { get; init; }

    [JsonPropertyOrder(6)]
    public required List<Conflict> Conflicts { get; init; }

    [JsonPropertyOrder(7)]
    public required List<GeneratedView> Generated { get; init; }

    [JsonPropertyOrder(8)]
    public required UnavailableCoverage Coverage { get; init; }

    [JsonPropertyOrder(9)]
    public required AdjudicationReport Adjudication { get; init; }

    [JsonPropertyOrder(10)]
    public required SurfaceReport Surface { get; init; }

    // `object`: System.Text.Json writes an object-typed value by its RUNTIME type.
    [JsonPropertyOrder(11)]
    public required List<object> Questions { get; init; }

    [JsonPropertyOrder(12)]
    public required RequiresView Requires { get; init; }

    IReadOnlyList<IBehaviourView> IProjectView.Behaviours => Behaviours;

    IReadOnlyList<IConflict> IProjectView.Conflicts => Conflicts;

    IReadOnlyList<IGeneratedView> IProjectView.Generated => Generated;

    IUnavailableCoverage IProjectView.Coverage => Coverage;

    IAdjudicationReport IProjectView.Adjudication => Adjudication;

    ISurfaceReport IProjectView.Surface => Surface;

    IReadOnlyList<IQuestion> IProjectView.Questions => Questions.Cast<IQuestion>().ToList();

    IRequiresView IProjectView.Requires => Requires;
}
