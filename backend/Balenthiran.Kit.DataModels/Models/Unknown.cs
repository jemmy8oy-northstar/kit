using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>
/// The same hole as it appears on the behaviour's `unknowns`, which carries the
/// line it was found on. Deliberately a separate type from <see cref="Hole"/>:
/// the two shapes differ by exactly one key in the goldens, and sharing one type
/// with an optional `at` would make a missing `at` serialise as `null` rather
/// than be absent, which is a different document.
/// </summary>
public sealed class Unknown : IUnknown
{
    [JsonPropertyOrder(1)]
    public required string Slot { get; init; }

    [JsonPropertyOrder(2)]
    public required string At { get; init; }
}
