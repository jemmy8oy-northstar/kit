using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>
/// What the router answers — the shape <c>ui.js</c>'s <c>route()</c> returns: a JSON
/// payload in <see cref="Body"/>, or bytes from the bundle in <see cref="Raw"/>, never both.
/// </summary>
public sealed class KitResponse : IKitResponse
{
    [JsonPropertyOrder(1)]
    public required int Status { get; init; }

    [JsonPropertyOrder(2)]
    public required string ContentType { get; init; }

    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CacheControl { get; init; }

    // `object`: System.Text.Json writes an object-typed value by its RUNTIME type.
    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Body { get; init; }

    // Bytes are delivered as they are, never serialised.
    [JsonIgnore]
    public byte[]? Raw { get; init; }

    // A header, not part of the payload the read golden records.
    [JsonIgnore]
    public string? SetCookie { get; init; }
}
