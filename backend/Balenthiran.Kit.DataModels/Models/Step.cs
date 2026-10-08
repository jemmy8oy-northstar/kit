using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A step: a verb plus noun references. `?slot` marks a hole.</summary>
public sealed class Step : IStep
{
    [JsonPropertyOrder(1)]
    public required string Kind { get; init; }

    [JsonPropertyOrder(2)]
    public required string Verb { get; init; }

    [JsonPropertyOrder(3)]
    public required string Text { get; init; }

    [JsonPropertyOrder(4)]
    public List<Reference> Refs { get; init; } = [];

    [JsonPropertyOrder(5)]
    public List<Hole> Holes { get; init; } = [];

    [JsonPropertyOrder(6)]
    public required string At { get; init; }

    // Written by `resolve`, ABSENT (not null) before it — the `parse` section of every
    // golden has no `resolved` key, and `kit.js` adds it lazily, after `at`. Ordered,
    // because a step with two filled holes writes them in fill order. Each value is
    // the SAME list the symbol holds, as in `kit.js`, not a copy.
    [JsonPropertyOrder(7)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OrderedDictionary<string, IReadOnlyList<string>>? Resolved { get; set; }

    // The interface view. Explicit, so System.Text.Json — which serialises
    // public members only — never sees it, and the wire shape stays the
    // concrete properties above with their stated order.
    IReadOnlyList<IReference> IStep.Refs => Refs;
    IReadOnlyList<IHole> IStep.Holes => Holes;
    IReadOnlyDictionary<string, IReadOnlyList<string>>? IStep.Resolved => Resolved;
}
