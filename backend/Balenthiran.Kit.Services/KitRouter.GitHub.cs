using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-USERS-1's second half: once signed in, a user connects their GitHub by installing Kit's
/// GitHub App on their own account and then authorising it. The page sends back the code GitHub
/// returned, and the installation recorded is the one on the account that authorised; the
/// <c>installation_id</c> on the install redirect is never read, because anyone can type one.
/// With no <see cref="IGitHubConnect"/> none of this is reachable.
/// </summary>
public sealed partial class KitRouter
{
    // Long enough to install the App on an organisation and come back; short enough that a
    // state copied out of a page is soon worth nothing.
    private static readonly TimeSpan ConnectLife = TimeSpan.FromMinutes(30);

    /// <summary>The session's GitHub fields: only for a signed-in user who is not the operator.</summary>
    private (string? Account, string? Install, string? Authorize) GitHubOf(string? who)
    {
        if (users is null || who is null || who == ISessionStore.Operator)
        {
            return (null, null, null);
        }

        var account = users.GitHubOf(who)?.Account;
        return github is null
            ? (account, null, null)
            : (account, github.InstallUrl, github.AuthorizeUrl(ConnectState(who, Now() + ConnectLife)));
    }

    /// <summary><c>/api/github/connected</c>: the page hands back what GitHub returned.</summary>
    private KitResponse ConnectGitHub(string who, string? cookie, JsonElement? body)
    {
        if (users is null || github is null)
        {
            return Json(404, new ApiError { Error = "no-such-route", Reason = "this Kit cannot connect a GitHub: its App has no OAuth client configured" });
        }

        if (who == ISessionStore.Operator)
        {
            return Json(409, new ApiError { Error = "operator-github", Reason = "the operator's GitHub is the App installation Kit is deployed with, so it is changed there" });
        }

        var b = body ?? default;
        if (!ValidConnectState(Str(b, "state"), who))
        {
            return Json(403, new ApiError { Error = "bad-state", Reason = "this GitHub authorisation was not started from this account in the last 30 minutes, so Kit will not connect it: connect again" });
        }

        if (Str(b, "code") is not { Length: > 0 } code)
        {
            return Json(400, new ApiError { Error = "no-code", Reason = "GitHub's authorisation code is missing" });
        }

        IGitHubLink? link;
        try
        {
            // Sync over async, as Commit does: the host has no synchronisation context to deadlock on.
            link = github.InstallationAsync(code).GetAwaiter().GetResult();
        }
        catch (GitHubConnectException e)
        {
            return Json(502, new ApiError { Error = e.ErrorCode, Reason = e.Message });
        }

        if (link is null)
        {
            return Json(409, new ApiError { Error = "no-installation", Reason = "Kit's GitHub App is not installed on your own GitHub account (an organisation's is not connected yet): install it there, then connect" });
        }

        return users.SetGitHub(who, link)
            ? Json(200, SessionOf(cookie))
            : Json(409, new ApiError { Error = "github-taken", Reason = $"{link.Account} is already connected to another Kit user" });
    }

    /// <summary>
    /// BEH-USERS-10: the GitHub sign-in link for someone signed out, or null when this Kit has
    /// no accounts or cannot connect a GitHub. Its state is signed for signing in, so a state
    /// made to connect one user's GitHub never signs anyone in, nor the reverse.
    /// </summary>
    private string? GitHubSignInUrl() =>
        users is null || github is null ? null : github.AuthorizeUrl(SignInState(Now() + ConnectLife));

    /// <summary>
    /// A user who has connected a GitHub signs in with it, and only with it. Only while this Kit
    /// can connect one: a Kit deployed without its OAuth client still takes the password, or
    /// they could not sign in at all.
    /// </summary>
    private bool SignsInWithGitHub(string who) => github is not null && users?.GitHubOf(who) is not null;

    /// <summary>
    /// <c>/api/session/github</c>: the page hands back what GitHub returned to someone signed
    /// out. The account that authorised is matched to the one user whose connected installation
    /// is on it; a GitHub no user has connected is refused, so there is still no open sign-up.
    /// </summary>
    private KitResponse SignInWithGitHub(JsonElement? body)
    {
        if (!lockRequired || users is null || github is null)
        {
            return Json(404, new ApiError { Error = "no-such-route", Reason = "this Kit cannot sign anyone in with GitHub: it needs accounts and its App's OAuth client" });
        }

        var b = body ?? default;
        if (!ValidSignInState(Str(b, "state")))
        {
            return Json(403, new ApiError { Error = "bad-state", Reason = "this GitHub sign-in was not started from Kit in the last 30 minutes: sign in again" });
        }

        if (Str(b, "code") is not { Length: > 0 } code)
        {
            return Json(400, new ApiError { Error = "no-code", Reason = "GitHub's authorisation code is missing" });
        }

        IGitHubLink? link;
        try
        {
            // Sync over async, as Commit does: the host has no synchronisation context to deadlock on.
            link = github.InstallationAsync(code).GetAwaiter().GetResult();
        }
        catch (GitHubConnectException e)
        {
            return Json(502, new ApiError { Error = e.ErrorCode, Reason = e.Message });
        }

        if (link is null || users.UserOfInstallation(link.Installation) is not { } who)
        {
            return Json(401, new ApiError { Error = "github-not-connected", Reason = "no Kit user has connected this GitHub: sign in with the user name and password you were given, then connect it in Settings" });
        }

        var token = sessions!.Create(who);
        return new KitResponse { Status = 200, ContentType = "application/json", Body = new SignInState { Ok = true, SignedIn = true }, SetCookie = CookieFor(token) };
    }

    /// <summary>
    /// BEH-USERS-2: the repos of the user's connected GitHub that carry a corpus. No link, or a
    /// Kit that cannot read one, answers an empty list as before; a GitHub that cannot be read
    /// says so in <c>githubError</c>, because an empty list would read as "you have no projects".
    /// </summary>
    private KitResponse UsersProjects(string who)
    {
        if (userProjects is null || users?.GitHubOf(who) is not { } link)
        {
            return Json(200, new ProjectList { Projects = [] });
        }

        try
        {
            // Sync over async, as Commit does: the host has no synchronisation context to deadlock on.
            var found = userProjects.ProjectsAsync(link.Installation).GetAwaiter().GetResult();
            return Json(200, new ProjectList
            {
                Projects = [],
                GitHub = found.Select(p => new UserProject { Name = p.Name, FullName = p.FullName, Url = p.Url, Corpus = p.Corpus.ToList() }).ToList(),
            });
        }
        catch (GitHubReadException e)
        {
            return Json(200, new ProjectList { Projects = [], GitHubError = e.Message });
        }
    }

    private DateTimeOffset Now() => (clock ?? TimeProvider.System).GetUtcNow();

    /// <summary><c>expires.signature</c>: names no one, but only verifies for the user it was made for.</summary>
    private string ConnectState(string who, DateTimeOffset expires)
    {
        var at = expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return $"{at}.{ConnectSignature(who, at)}";
    }

    private bool ValidConnectState(string? state, string who)
    {
        var parts = state?.Split('.');
        if (parts is not [var at, var signature]
            || !long.TryParse(at, NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
            || Now().ToUnixTimeSeconds() > expires)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(signature), Encoding.ASCII.GetBytes(ConnectSignature(who, at)));
    }

    // Keyed off KIT_PASSWORD, which accounts require, so a deploy does not void a state mid-install
    // and no new secret is needed; labelled, so it is never the same MAC as a session's.
    private string ConnectSignature(string who, string at) => Convert.ToHexStringLower(HMACSHA256.HashData(
        SHA256.HashData(Encoding.UTF8.GetBytes($"kit github connect\n{password}")),
        Encoding.UTF8.GetBytes($"{who}\n{at}")));

    /// <summary><c>in.expires.signature</c>: the prefix tells the page it is a sign-in coming back, not a connect.</summary>
    private string SignInState(DateTimeOffset expires)
    {
        var at = expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return $"in.{at}.{SignInSignature(at)}";
    }

    private bool ValidSignInState(string? state)
    {
        var parts = state?.Split('.');
        if (parts is not ["in", var at, var signature]
            || !long.TryParse(at, NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
            || Now().ToUnixTimeSeconds() > expires)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(signature), Encoding.ASCII.GetBytes(SignInSignature(at)));
    }

    // Its own label, so no connect state (made for a user) verifies as a sign-in, nor the reverse.
    private string SignInSignature(string at) => Convert.ToHexStringLower(HMACSHA256.HashData(
        SHA256.HashData(Encoding.UTF8.GetBytes($"kit github sign-in\n{password}")),
        Encoding.UTF8.GetBytes(at)));
}
