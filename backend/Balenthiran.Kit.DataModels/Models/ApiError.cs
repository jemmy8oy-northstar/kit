using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>A refusal: a code, a reason, and — for two of them — what would have been accepted.</summary>
public sealed class ApiError : IApiError
{
    [JsonPropertyOrder(1)]
    public required string Error { get; init; }

    [JsonPropertyOrder(2)]
    public required string Reason { get; init; }

    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Allow { get; init; }

    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Known { get; init; }

    IReadOnlyList<string>? IApiError.Known => Known;
}
