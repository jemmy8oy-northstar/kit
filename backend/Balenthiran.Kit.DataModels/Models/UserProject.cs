using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One row of <c>github</c> on <c>GET /api/projects</c> (BEH-USERS-2): a repo in the user's own GitHub that carries a corpus.</summary>
public sealed class UserProject : IUserProject
{
    [JsonPropertyOrder(1)]
    public required string Name { get; init; }

    [JsonPropertyOrder(2)]
    public required string FullName { get; init; }

    [JsonPropertyOrder(3)]
    public required string Url { get; init; }

    [JsonPropertyOrder(4)]
    public required List<string> Corpus { get; init; }

    IReadOnlyList<string> IUserProject.Corpus => Corpus;
}
