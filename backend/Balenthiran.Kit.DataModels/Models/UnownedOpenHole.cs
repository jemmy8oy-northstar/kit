using System.Text.Json.Serialization;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>An open hole whose step names only literals: <c>{ slot, key: "?slot", at }</c>. See <see cref="OpenHole"/>.</summary>
public sealed class UnownedOpenHole : OpenHole
{
    [JsonPropertyOrder(1)]
    public override required string Slot { get; init; }

    [JsonPropertyOrder(2)]
    public override required string Key { get; init; }

    [JsonPropertyOrder(3)]
    public override required string At { get; init; }
}
