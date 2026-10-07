using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>What the router answers: a status, a content type and a body — the shape `ui.js`'s `route()` returns.</summary>
public sealed class KitResponse : IKitResponse
{
    [JsonPropertyOrder(1)]
    public required int Status { get; init; }

    [JsonPropertyOrder(2)]
    public required string ContentType { get; init; }

    // `object`: System.Text.Json writes an object-typed value by its RUNTIME type.
    [JsonPropertyOrder(3)]
    public required object Body { get; init; }
}
