using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>
/// The 200 answer to a write, in <c>ui.js</c>'s key order. With git write-back on, the answer
/// is a <see cref="GitWriteOutcome"/>, whose fields slot into the gaps left in the order below.
/// </summary>
public class WriteOutcome : IWriteOutcome
{
    [JsonPropertyOrder(1)]
    public bool Ok { get; init; } = true;

    [JsonPropertyOrder(2)]
    public required string App { get; init; }

    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Behaviour { get; init; }

    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Noun { get; init; }

    [JsonPropertyOrder(5)]
    public required string File { get; init; }

    [JsonPropertyOrder(6)]
    public bool Committed { get; init; }

    [JsonPropertyOrder(10)]
    public required string Note { get; init; }

    [JsonPropertyOrder(12)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SharedWith { get; init; }

    [JsonPropertyOrder(13)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? UnreadableCorpora { get; init; }

    IReadOnlyList<string>? IWriteOutcome.SharedWith => SharedWith;

    IReadOnlyList<string>? IWriteOutcome.UnreadableCorpora => UnreadableCorpora;
}
