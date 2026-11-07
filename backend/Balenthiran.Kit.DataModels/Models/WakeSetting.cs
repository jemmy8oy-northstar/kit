using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>When the operator last asked the agent to wake (BEH-SETTINGS-7).</summary>
public sealed class WakeSetting : IWakeSetting
{
    [JsonPropertyOrder(1)]
    public string? Requested { get; init; }
}
