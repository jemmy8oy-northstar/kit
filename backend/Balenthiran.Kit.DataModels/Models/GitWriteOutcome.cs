using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>
/// <see cref="WriteOutcome"/> with git write-back on. <see cref="Commit"/> and <see cref="Branch"/>
/// are written even when null, as <c>ui.js</c> writes them; with git off the plain
/// <see cref="WriteOutcome"/> is answered and they are absent — that difference is the contract.
/// </summary>
public sealed class GitWriteOutcome : WriteOutcome, IGitWriteOutcome
{
    [JsonPropertyOrder(7)]
    public bool Pushed { get; init; }

    [JsonPropertyOrder(8)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Commit { get; init; }

    [JsonPropertyOrder(9)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Branch { get; init; }
}
