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

    /// <summary>Set only when this Kit has accounts (BEH-USERS-1), so the page asks for a user name.</summary>
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Accounts { get; init; }

    /// <summary>Who is signed in, when this Kit has accounts and someone is.</summary>
    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? User { get; init; }
}
