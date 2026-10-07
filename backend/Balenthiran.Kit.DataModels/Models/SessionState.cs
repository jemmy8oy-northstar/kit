using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>Whether a lock exists, and whether this caller is past it.</summary>
public sealed class SessionState : ISessionState
{
    [JsonPropertyOrder(1)]
    public required bool Required { get; init; }

    [JsonPropertyOrder(2)]
    public required bool SignedIn { get; init; }
}
