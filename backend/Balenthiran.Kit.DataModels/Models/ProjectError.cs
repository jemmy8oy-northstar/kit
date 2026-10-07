using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A list row for a corpus that could not be projected — reported AS an error, never as zero behaviours.</summary>
public sealed class ProjectError : IProjectError
{
    [JsonPropertyOrder(1)]
    public required string App { get; init; }

    [JsonPropertyOrder(2)]
    public required string Error { get; init; }
}
