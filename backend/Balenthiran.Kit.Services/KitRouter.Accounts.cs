using System.Text.Json;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Early-access accounts (BEH-USERS-1) and the check that keeps one user out of another's work
/// (BEH-USERS-8). James creates each account; its user changes the password he gave them. With
/// no <see cref="IUserStore"/> none of this is reachable and Kit answers as it did before.
/// </summary>
public sealed partial class KitRouter
{
    /// <summary>The operator's user name when nothing configures it.</summary>
    public const string DefaultOperator = "james";

    // Lower case, so "Ann" and "ann" are not two accounts; no dot, which a signed token splits on;
    // no "@", which marks the operator's session.
    private static readonly Regex UserName = new(@"^[a-z0-9][a-z0-9_-]{1,31}\z", RegexOptions.Compiled);

    private SessionState SessionOf(string? cookie)
    {
        var who = lockRequired ? UserOf(cookie) : null;
        var gh = GitHubOf(who);
        return new SessionState
        {
            Required = lockRequired,
            SignedIn = !lockRequired || who is not null,

            // Absent with accounts off, so the answer is byte-for-byte what it was (the auth goldens).
            Accounts = users is null ? null : true,
            User = users is null || who is null ? null : who == ISessionStore.Operator ? operatorName : who,
            Operator = users is not null && who == ISessionStore.Operator ? true : null,
            GitHub = gh.Account,
            GitHubInstall = gh.Install,
            GitHubAuthorize = gh.Authorize,
            GitHubSignIn = lockRequired && who is null ? GitHubSignInUrl() : null,
        };
    }

    /// <summary>Fails closed: no store means nobody is signed in.</summary>
    private string? UserOf(string? cookie) => sessions?.UserOf(ParseCookies(cookie).GetValueOrDefault(Cookie));

    /// <summary>Signed in as an early-access user rather than the operator.</summary>
    private bool IsOtherUser(string? cookie) => users is not null && UserOf(cookie) is { } who && who != ISessionStore.Operator;

    private bool IsUsersPassword(string name, string given)
    {
        // An invalid name is checked against nothing, at the same cost as a real one.
        var hash = users is not null && UserName.IsMatch(name) ? users.HashOf(name) : null;
        return PasswordHash.Verify(given, hash);
    }

    /// <summary><c>/api/users</c> (he creates an account) and <c>/api/session/password</c> (a user changes theirs).</summary>
    private KitResponse Account(string pathname, string who, JsonElement? body)
    {
        if (users is null)
        {
            return Json(404, new ApiError { Error = "no-such-route", Reason = "this Kit has no accounts: only the operator's password signs in" });
        }

        var b = body ?? default;
        return pathname == "/api/users" ? CreateUser(who, b) : ChangePassword(who, b);
    }

    private KitResponse CreateUser(string who, JsonElement body)
    {
        if (who != ISessionStore.Operator)
        {
            return Json(403, new ApiError { Error = "not-operator", Reason = "only the operator creates accounts" });
        }

        var name = Str(body, "username");
        if (name is null || !UserName.IsMatch(name))
        {
            return Json(400, new ApiError { Error = "bad-username", Reason = "a user name is 2 to 32 characters: lower-case letters, digits, '-' and '_', starting with a letter or digit" });
        }

        var password = Str(body, "password");
        if (PasswordHash.Refuse(password) is { } why)
        {
            return Json(400, new ApiError { Error = "bad-new-password", Reason = why });
        }

        // His own name is taken by him, though he is not in the store.
        if (name == operatorName || !users!.Add(name, PasswordHash.Hash(password!)))
        {
            return Json(409, new ApiError { Error = "user-exists", Reason = $"there is already a user named '{name}'" });
        }

        return Json(201, new AccountState { Ok = true, User = name });
    }

    private KitResponse ChangePassword(string who, JsonElement body)
    {
        if (who == ISessionStore.Operator)
        {
            return Json(409, new ApiError { Error = "operator-password", Reason = "the operator's password is KIT_PASSWORD, set where Kit is deployed, so it is changed there" });
        }

        // BEH-USERS-10: a password that no longer signs anyone in is not worth changing.
        if (SignsInWithGitHub(who))
        {
            return Json(409, new ApiError { Error = "github-sign-in", Reason = "this account signs in with GitHub now, so it has no password to change" });
        }

        var next = Str(body, "next");
        if (PasswordHash.Refuse(next) is { } why)
        {
            return Json(400, new ApiError { Error = "bad-new-password", Reason = why });
        }

        // The current password too, so a session left open on a shared screen cannot take the account.
        // Throttled like sign-in, because without that it is a guessing loop for whoever holds the cookie.
        lock (signInGate)
        {
            var wait = throttle?.RetryAfterMs() ?? 0;
            if (wait > 0)
            {
                var seconds = (int)Math.Ceiling(wait / 1000.0);
                return Json(429, new ApiError { Error = "too-many-attempts", Reason = $"too many failed attempts; try again in {seconds}s", RetryAfterSeconds = seconds });
            }

            if (!IsUsersPassword(who, Str(body, "current") ?? string.Empty))
            {
                throttle?.Fail();
                return Json(401, new ApiError { Error = "bad-password", Reason = "that is not the current password" });
            }

            throttle?.Succeed();
        }

        return users!.SetHash(who, PasswordHash.Hash(next!))
            ? Json(200, new AccountState { Ok = true, User = who })
            : Json(404, new ApiError { Error = "no-such-user", Reason = "this account no longer exists" });
    }
}
