using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="INote"/>
public sealed class Note : INote
{
    [JsonPropertyOrder(1)]
    public required string At { get; init; }

    [JsonPropertyOrder(2)]
    public string Kind { get; init; } = "note";

    [JsonPropertyOrder(3)]
    public string? Behaviour { get; init; }

    [JsonPropertyOrder(4)]
    public string? Resolves { get; init; }

    [JsonPropertyOrder(5)]
    public required string Text { get; init; }

    [JsonPropertyOrder(6)]
    public List<NoteEntry> Thread { get; init; } = [];

    [JsonPropertyOrder(7)]
    public List<string> Questions { get; init; } = [];

    IReadOnlyList<INoteEntry> INote.Thread => Thread;

    IReadOnlyList<string> INote.Questions => Questions;
}
