using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// A user's projects, read from their own GitHub (BEH-USERS-2): a project is a repo Kit's App is
/// installed on that carries a corpus, so Kit keeps no list of its own.
/// </summary>
public interface IUserProjects
{
    /// <summary>
    /// The repos of <paramref name="installation"/> that carry at least one <c>.beh</c> file on
    /// their default branch. Throws <see cref="Exceptions.GitHubReadException"/> when GitHub could
    /// not be read — could-not-look, never an empty list.
    /// </summary>
    Task<IReadOnlyList<IUserProject>> ProjectsAsync(long installation, CancellationToken cancellationToken = default);
}
