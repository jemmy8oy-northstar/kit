using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One behaviour's generated test, keyed by its id.</summary>
public sealed class GeneratedView : IGeneratedView
{
    [JsonPropertyOrder(1)]
    public required string Id { get; init; }

    [JsonPropertyOrder(2)]
    public required string Code { get; init; }

    [JsonPropertyOrder(3)]
    public required List<string> Missing { get; init; }

    [JsonPropertyOrder(4)]
    public required GenerateStats Stats { get; init; }

    IReadOnlyList<string> IGeneratedView.Missing => Missing;

    IGenerateStats IGeneratedView.Stats => Stats;
}
