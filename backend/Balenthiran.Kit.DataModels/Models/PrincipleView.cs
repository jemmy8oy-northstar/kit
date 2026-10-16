using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <inheritdoc cref="IPrincipleView"/>
public sealed class PrincipleView : IPrincipleView
{
    [JsonPropertyOrder(1)]
    public required string Name { get; init; }

    [JsonPropertyOrder(2)]
    public required string Text { get; init; }

    [JsonPropertyOrder(3)]
    public required bool Global { get; init; }

    [JsonPropertyOrder(4)]
    public required string At { get; init; }
}
