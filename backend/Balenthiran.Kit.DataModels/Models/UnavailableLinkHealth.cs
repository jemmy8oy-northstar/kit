using System.Text.Json.Serialization;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Test links Kit could not check (BEH-HEALTH-4), and why.</summary>
public sealed class UnavailableLinkHealth
{
    [JsonPropertyOrder(1)]
    public required bool Available { get; init; }

    [JsonPropertyOrder(2)]
    public required string Reason { get; init; }
}
