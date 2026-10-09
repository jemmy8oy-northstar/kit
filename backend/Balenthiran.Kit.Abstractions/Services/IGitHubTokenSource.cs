namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// The credential Kit sends GitHub (kit#88). Either the static <c>KIT_GIT_TOKEN</c>, or the
/// GitHub App installation process.md settles on — app id, installation id and private key,
/// EXCHANGED for a short-lived token and never sent as one.
/// </summary>
public interface IGitHubTokenSource
{
    /// <summary>
    /// The token to send now, or null when none is configured. Throws
    /// <see cref="Exceptions.GitHubTokenException"/> when one is configured but could not be
    /// obtained; its message is a reason a reader may be shown, and never holds a secret.
    /// </summary>
    Task<string?> TokenAsync(CancellationToken cancellationToken = default);
}
