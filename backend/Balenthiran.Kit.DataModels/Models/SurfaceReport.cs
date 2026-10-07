using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Which inferences serve a documented behaviour, which serve nothing, and every broken `serves` link.</summary>
public sealed class SurfaceReport : ISurfaceReport
{
    [JsonPropertyOrder(1)]
    public required List<string> Errors { get; init; }

    [JsonPropertyOrder(2)]
    public required List<string> Served { get; init; }

    [JsonPropertyOrder(3)]
    public required List<string> Unserved { get; init; }

    IReadOnlyList<string> ISurfaceReport.Errors => Errors;

    IReadOnlyList<string> ISurfaceReport.Served => Served;

    IReadOnlyList<string> ISurfaceReport.Unserved => Unserved;
}
