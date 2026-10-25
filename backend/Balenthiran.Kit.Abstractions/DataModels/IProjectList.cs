namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The body of `GET /api/projects`. Rows are a summary or an error, so they are written by runtime type.</summary>
public interface IProjectList
{
    IReadOnlyList<object> Projects { get; }

    /// <summary>Set only for a user with a connected GitHub on a Kit that can read it (BEH-USERS-2): their repos that carry a corpus.</summary>
    IReadOnlyList<IUserProject>? GitHub { get; }

    /// <summary>Set instead of <see cref="GitHub"/> when GitHub could not be read: why, so a failure never shows as "no projects".</summary>
    string? GitHubError { get; }
}
