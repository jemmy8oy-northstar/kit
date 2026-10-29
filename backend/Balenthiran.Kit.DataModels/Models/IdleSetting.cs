using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>How busy the agent should be when nothing is asked of it (BEH-SETTINGS-5).</summary>
public sealed class IdleSetting : IIdleSetting
{
    [JsonPropertyOrder(1)]
    public required string Idle { get; init; }
}
