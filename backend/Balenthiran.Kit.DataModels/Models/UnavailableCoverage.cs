using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Coverage that could not be read, and why. Hosted Kit reads no repository, so this is the only shape it serves.</summary>
public sealed class UnavailableCoverage : IUnavailableCoverage
{
    [JsonPropertyOrder(1)]
    public required bool Available { get; init; }

    [JsonPropertyOrder(2)]
    public required string Reason { get; init; }
}
