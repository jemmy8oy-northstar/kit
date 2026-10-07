using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>The requires report with each noun decorated for the page.</summary>
public sealed class RequiresView : IRequiresView
{
    [JsonPropertyOrder(1)]
    public required List<NounView> Nouns { get; init; }

    [JsonPropertyOrder(2)]
    public required List<NounView> Missing { get; init; }

    [JsonPropertyOrder(3)]
    public required List<NounView> Insufficient { get; init; }

    [JsonPropertyOrder(4)]
    public required List<string> Satisfied { get; init; }

    IReadOnlyList<INounView> IRequiresView.Nouns => Nouns;

    IReadOnlyList<INounView> IRequiresView.Missing => Missing;

    IReadOnlyList<INounView> IRequiresView.Insufficient => Insufficient;

    IReadOnlyList<string> IRequiresView.Satisfied => Satisfied;
}
