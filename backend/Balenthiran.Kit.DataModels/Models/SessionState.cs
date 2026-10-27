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

    /// <summary>True when the one signed in is the operator, so the page offers him "add a user"; absent otherwise.</summary>
    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Operator { get; init; }

    /// <summary>The GitHub account a signed-in user has connected; absent until they do.</summary>
    [JsonPropertyOrder(6)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GitHub { get; init; }

    /// <summary>Where a signed-in user installs Kit's GitHub App; absent for the operator, or when this Kit cannot connect one.</summary>
    [JsonPropertyOrder(7)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GitHubInstall { get; init; }

    /// <summary>Where they authorise it afterwards, carrying a state signed for this user alone.</summary>
    [JsonPropertyOrder(8)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GitHubAuthorize { get; init; }

    /// <summary>
    /// BEH-USERS-10: where someone signed out signs in with GitHub, carrying a state signed for
    /// signing in. Absent once signed in, or when this Kit has no accounts or cannot connect a GitHub.
    /// </summary>
    [JsonPropertyOrder(9)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GitHubSignIn { get; init; }
}
