using System.Text.Json.Serialization;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>The body of `GET /api/projects`. Rows are a summary or an error, so they are written by runtime type.</summary>
public sealed class ProjectList : IProjectList
{
    // `object`: System.Text.Json writes an object-typed value by its RUNTIME type.
    [JsonPropertyOrder(1)]
    public required List<object> Projects { get; init; }

    /// <summary>Absent from the body unless set, so the operator's list is byte for byte what it was.</summary>
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<UserProject>? GitHub { get; init; }

    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GitHubError { get; init; }

    IReadOnlyList<object> IProjectList.Projects => Projects;

    IReadOnlyList<IUserProject>? IProjectList.GitHub => GitHub;
}
