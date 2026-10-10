using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="INoteList"/>
public sealed class NoteList : INoteList
{
    [JsonPropertyOrder(1)]
    public required string App { get; init; }

    [JsonPropertyOrder(2)]
    public required string File { get; init; }

    [JsonPropertyOrder(3)]
    public required List<Note> Notes { get; init; }

    IReadOnlyList<INote> INoteList.Notes => Notes;
}
