using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// How an early-access user connects their GitHub (BEH-USERS-1): they install Kit's GitHub App
/// on their own account, then authorise it, and the installation Kit records is the one on the
/// account that authorised. The <c>installation_id</c> GitHub puts on the install redirect is
/// never read — GitHub's docs say it can be forged — and an organisation's installation is not
/// taken either, because any member can reach one.
/// </summary>
public interface IGitHubConnect
{
    /// <summary>The page a user installs Kit's App from.</summary>
    string InstallUrl { get; }

    /// <summary>The page a user authorises Kit's App on; GitHub hands <paramref name="state"/> back unchanged.</summary>
    string AuthorizeUrl(string state);

    /// <summary>
    /// The installation of Kit's App on the personal account of whoever authorised
    /// <paramref name="code"/>, or null when there is none; the operator's own is never returned.
    /// Throws <see cref="Exceptions.GitHubConnectException"/> for a refused code or a GitHub
    /// failure. The user token is used for this and revoked; it is never kept.
    /// </summary>
    Task<IGitHubLink?> InstallationAsync(string code, CancellationToken cancellationToken = default);
}
