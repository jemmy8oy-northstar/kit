using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="ITriageAnswer"/>
public sealed class TriageAnswer : ITriageAnswer
{
    [JsonPropertyOrder(1)]
    public required string Reply { get; init; }

    [JsonPropertyOrder(2)]
    public required int Remaining { get; init; }

    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TriageAction? Action { get; init; }

    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Options { get; init; }

    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Truncated { get; init; }
}
