using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// How an early-access user connects their GitHub (BEH-USERS-1): they install Kit's GitHub App,
/// then authorise it, and the installations that authorisation can reach are the ones Kit
/// records as theirs. The <c>installation_id</c> GitHub puts on the install redirect is never
/// trusted on its own — GitHub's docs say it can be spoofed — so an installation is only ever
/// taken from what a user access token lists.
/// </summary>
public interface IGitHubConnect
{
    /// <summary>The page a user installs Kit's App from.</summary>
    string InstallUrl { get; }

    /// <summary>The page a user authorises Kit's App on; GitHub hands <paramref name="state"/> back unchanged.</summary>
    string AuthorizeUrl(string state);

    /// <summary>
    /// The installations of Kit's App that the user who authorised <paramref name="code"/> can
    /// reach, the operator's own excluded. Throws <see cref="Exceptions.GitHubConnectException"/>
    /// for a refused code or a GitHub failure; the user token is used once and never kept.
    /// </summary>
    Task<IReadOnlyList<IGitHubLink>> InstallationsAsync(string code, CancellationToken cancellationToken = default);
}
