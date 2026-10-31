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

    // kit#237: the board sorts by these. After `at`, so every key already served keeps its place.
    [JsonPropertyOrder(11)]
    public required bool Pending { get; init; }

    [JsonPropertyOrder(12)]
    public required string Layer { get; init; }

    // kit#237: the projects view draws a behaviour under each feature named here. Always
    // written, empty when untagged, after `layer` — the rule `pending` and `layer` follow.
    [JsonPropertyOrder(13)]
    public required List<string> In { get; init; }

    // kit#284: the board orders Proposed and Backlog by it. Absent without a `priority` line,
    // so no response that predates it changes shape.
    [JsonPropertyOrder(14)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Priority { get; init; }

    // BEH-BOARD-16: the board holds a paused card at the foot of Backlog. Absent when false.
    [JsonPropertyOrder(15)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Paused { get; init; }

    // BEH-BOARD-26: the date a paused card should be asked about again. Absent for a bare `paused`.
    [JsonPropertyOrder(30)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PausedUntil { get; init; }

    // BEH-BOARD-17: what he must add, from a `needs-context` line. Absent when there is none.
    [JsonPropertyOrder(16)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NeedsContext { get; init; }

    // BEH-BOARD-22: built on dev, not yet live, from an `on-dev` line. Absent when there is none.
    [JsonPropertyOrder(17)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OnDev { get; init; }

    IReadOnlyList<string> IBehaviourView.In => In;

    IReadOnlyList<IStepView> IBehaviourView.Steps => Steps;

    IReadOnlyList<string> IBehaviourView.Open => Open;

    IReadOnlyList<IFilledView> IBehaviourView.Filled => Filled;

    ISource IBehaviourView.Source => Source;

    IReview IBehaviourView.Review => Review;
}
