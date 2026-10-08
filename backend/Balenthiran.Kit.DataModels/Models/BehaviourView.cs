using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One behaviour as the project view serves it.</summary>
public sealed class BehaviourView : IBehaviourView
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Title { get; init; }

    [JsonPropertyOrder(3)]
    public required string? Actor { get; init; }

    [JsonPropertyOrder(4)]
    public required List<StepView> Steps { get; init; }

    [JsonPropertyOrder(5)]
    public required List<string> Open { get; init; }

    [JsonPropertyOrder(6)]
    public required List<FilledView> Filled { get; init; }

    [JsonPropertyOrder(7)]
    public required Source Source { get; init; }

    [JsonPropertyOrder(8)]
    public required Review Review { get; init; }

    [JsonPropertyOrder(9)]
    public required string? Asks { get; init; }

    [JsonPropertyOrder(10)]
    public required string At { get; init; }

    IReadOnlyList<IStepView> IBehaviourView.Steps => Steps;

    IReadOnlyList<string> IBehaviourView.Open => Open;

    IReadOnlyList<IFilledView> IBehaviourView.Filled => Filled;

    ISource IBehaviourView.Source => Source;

    IReview IBehaviourView.Review => Review;
}
