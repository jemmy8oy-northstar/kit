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

    /// <summary>
    /// BEH-WELCOME-1: the text of each file on <paramref name="fullName"/>'s default branch whose path
    /// <paramref name="wanted"/> accepts, by path, read with <paramref name="installation"/>'s token, so a
    /// repo the installation does not reach cannot be read. Throws <see cref="Exceptions.GitHubReadException"/>
    /// when GitHub could not be read, or listed only part of the tree.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> FilesAsync(long installation, string fullName, Func<string, bool> wanted, CancellationToken cancellationToken = default);
}
