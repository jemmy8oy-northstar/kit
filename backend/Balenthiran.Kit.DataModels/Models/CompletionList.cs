using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="ICompletionList"/>
public sealed class CompletionList : ICompletionList
{
    [JsonPropertyOrder(1)]
    public required string App { get; init; }

    [JsonPropertyOrder(2)]
    public required string File { get; init; }

    [JsonPropertyOrder(3)]
    public required string History { get; init; }

    [JsonPropertyOrder(4)]
    public required string? Reason { get; init; }

    [JsonPropertyOrder(5)]
    public required List<Completion> Completed { get; init; }

    IReadOnlyList<ICompletion> ICompletionList.Completed => Completed;
}
