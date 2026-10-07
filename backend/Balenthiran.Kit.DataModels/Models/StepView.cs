using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A step as the project view serves it: what was written, without what resolve added.</summary>
public sealed class StepView : IStepView
{
    [JsonPropertyOrder(1)]
    public required string Kind { get; init; }

    [JsonPropertyOrder(2)]
    public required string Verb { get; init; }

    [JsonPropertyOrder(3)]
    public required string Text { get; init; }

    [JsonPropertyOrder(4)]
    public required List<Reference> Refs { get; init; }

    [JsonPropertyOrder(5)]
    public required List<Hole> Holes { get; init; }

    IReadOnlyList<IReference> IStepView.Refs => Refs;

    IReadOnlyList<IHole> IStepView.Holes => Holes;
}
