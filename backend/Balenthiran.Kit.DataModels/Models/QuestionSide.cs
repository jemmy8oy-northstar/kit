using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One side of a conflict, carrying its own citation and value so a reader needs no repo.</summary>
public sealed class QuestionSide : IQuestionSide
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Title { get; init; }

    [JsonPropertyOrder(3)]
    public required string? Ref { get; init; }

    [JsonPropertyOrder(4)]
    public required List<string> Value { get; init; }

    IReadOnlyList<string> IQuestionSide.Value => Value;
}
