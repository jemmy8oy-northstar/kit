using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Three populations kept apart: no binding, a binding that meets no verb, and generatable.</summary>
public sealed class RequiresReport : IRequiresReport
{
    [JsonPropertyOrder(1)]
    public required List<NounRequirement> Nouns { get; init; }

    [JsonPropertyOrder(2)]
    public required List<NounRequirement> Missing { get; init; }

    [JsonPropertyOrder(3)]
    public required List<NounRequirement> Insufficient { get; init; }

    [JsonPropertyOrder(4)]
    public required List<string> Satisfied { get; init; }

    IReadOnlyList<INounRequirement> IRequiresReport.Nouns => Nouns;

    IReadOnlyList<INounRequirement> IRequiresReport.Missing => Missing;

    IReadOnlyList<INounRequirement> IRequiresReport.Insufficient => Insufficient;

    IReadOnlyList<string> IRequiresReport.Satisfied => Satisfied;
}
