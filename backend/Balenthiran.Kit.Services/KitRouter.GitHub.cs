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
/// GitHub App and then authorising it. The page sends back the code GitHub returned, and the
/// installation recorded is one GitHub lists for the user who authorised: the
/// <c>installation_id</c> on the install redirect is a hint at most, because anyone can type one.
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

        IReadOnlyList<IGitHubLink> reachable;
        try
        {
            // Sync over async, as Commit does: the host has no synchronisation context to deadlock on.
            reachable = github.InstallationsAsync(code).GetAwaiter().GetResult();
        }
        catch (GitHubConnectException e)
        {
            return Json(502, new ApiError { Error = e.ErrorCode, Reason = e.Message });
        }

        IGitHubLink? chosen;
        if (InstallationHint(b) is { } hint)
        {
            chosen = reachable.FirstOrDefault(l => l.Installation == hint);
            if (chosen is null)
            {
                return Json(403, new ApiError { Error = "not-your-installation", Reason = $"GitHub does not list installation {hint} among those you can reach, so Kit will not connect it" });
            }
        }
        else if (reachable.Count == 1)
        {
            chosen = reachable[0];
        }
        else
        {
            return Json(409, reachable.Count == 0
                ? new ApiError { Error = "no-installation", Reason = "Kit's GitHub App is not installed on any account you can reach: install it, then connect" }
                : new ApiError { Error = "several-installations", Reason = $"Kit's GitHub App is installed on {string.Join(", ", reachable.Select(l => l.Account))}; install it from Kit's page again, so Kit knows which one is yours" });
        }

        return users.SetGitHub(who, chosen)
            ? Json(200, SessionOf(cookie))
            : Json(409, new ApiError { Error = "github-taken", Reason = $"{chosen.Account}'s installation is already connected to another Kit user" });
    }

    /// <summary>The installation GitHub's install redirect named, as the page passed it on: a number or its digits.</summary>
    private static long? InstallationHint(JsonElement body) => Field(body, "installation") switch
    {
        { ValueKind: JsonValueKind.Number } n when n.TryGetInt64(out var id) => id,
        { ValueKind: JsonValueKind.String } s when long.TryParse(s.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) => id,
        _ => null,
    };

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
}
