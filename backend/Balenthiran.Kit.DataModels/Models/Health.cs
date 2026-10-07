using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>The health check.</summary>
public sealed class Health : IHealth
{
    [JsonPropertyOrder(1)]
    public required bool Ok { get; init; }
}
