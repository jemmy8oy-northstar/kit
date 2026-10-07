using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A hole another behaviour filled, as the project view serves it.</summary>
public sealed class FilledView : IFilledView
{
    [JsonPropertyOrder(1)]
    public required string Key { get; init; }

    [JsonPropertyOrder(2)]
    public required List<string> Value { get; init; }

    IReadOnlyList<string> IFilledView.Value => Value;
}
