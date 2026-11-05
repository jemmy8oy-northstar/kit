using System.Text.Json.Serialization;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Test links that were checked (BEH-HEALTH-4). Empty <see cref="Broken"/> means every recorded link held.</summary>
public sealed class AvailableLinkHealth
{
    [JsonPropertyOrder(1)]
    public required bool Available { get; init; }

    [JsonPropertyOrder(2)]
    public required int Recorded { get; init; }

    [JsonPropertyOrder(3)]
    public required List<BrokenLink> Broken { get; init; }
}
