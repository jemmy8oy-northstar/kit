using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>The last PR that improved a project's test health (BEH-HEALTH-2): the shape <c>LastImprovedBy</c> in the UI's types reads.</summary>
public sealed class LastImprovedBy : ILastImprovedBy
{
    [JsonPropertyOrder(1)]
    public required string Pr { get; init; }

    [JsonPropertyOrder(2)]
    public required string Url { get; init; }

    [JsonPropertyOrder(3)]
    public required string On { get; init; }
}
