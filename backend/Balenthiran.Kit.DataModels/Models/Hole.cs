using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A hole as it appears on a step: the slot name alone.</summary>
public sealed class Hole : IHole
{
    [JsonPropertyOrder(1)]
    public required string Slot { get; init; }
}
