using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>What stage 3 returns for one behaviour. Key order is the goldens': code, missing, stats.</summary>
public sealed class GeneratedTest : IGeneratedTest
{
    [JsonPropertyOrder(1)]
    public required string Code { get; init; }

    [JsonPropertyOrder(2)]
    public required List<string> Missing { get; init; }

    [JsonPropertyOrder(3)]
    public required GenerateStats Stats { get; init; }

    IReadOnlyList<string> IGeneratedTest.Missing => Missing;

    IGenerateStats IGeneratedTest.Stats => Stats;
}
