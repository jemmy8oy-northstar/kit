using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>The agent's idle work, in his order (BEH-SETTINGS-6; BEH-SETTINGS-5's <c>idle</c> still served).</summary>
public sealed class IdleSetting : IIdleSetting
{
    [JsonPropertyOrder(1)]
    public required string Idle { get; init; }

    [JsonPropertyOrder(2)]
    public required IReadOnlyList<string> Order { get; init; }
}
