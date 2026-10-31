using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One Kit feature and its two switches (BEH-FLAGS-1).</summary>
public sealed class FeatureSwitch : IFeatureSwitch
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Title { get; init; }

    [JsonPropertyOrder(3)]
    public required bool Operator { get; init; }

    [JsonPropertyOrder(4)]
    public required bool Testers { get; init; }
}
