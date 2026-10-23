using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>An installation of Kit's GitHub App that a user has shown is theirs (BEH-USERS-1).</summary>
public sealed class GitHubLink : IGitHubLink
{
    [JsonPropertyOrder(1)]
    public required long Installation { get; init; }

    [JsonPropertyOrder(2)]
    public required string Account { get; init; }
}
