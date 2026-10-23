namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// The live session tokens (<c>auth.js</c>'s <c>sessions()</c>). A server with a password uses
/// <c>SignedSessionStore</c>, whose tokens survive a restart without a session table (the
/// database he parked on kit#41); the in-memory <c>SessionStore</c> is the oracle's.
/// </summary>
/// <remarks>
/// Every token names who it was minted for (BEH-USERS-8): the operator, or an early-access
/// user. A token minted before accounts existed is the operator's, so his phone stays signed in.
/// </remarks>
public interface ISessionStore
{
    /// <summary>The user a session minted by <see cref="Create()"/> belongs to. No user name can contain <c>@</c>.</summary>
    const string Operator = "@operator";

    /// <summary>Mint an operator token that is valid for the TTL.</summary>
    string Create();

    /// <summary>Mint a token for <paramref name="user"/> that is valid for the TTL.</summary>
    string Create(string user);

    /// <summary>Is this a token the store minted and has not yet expired or destroyed?</summary>
    bool Valid(string? token);

    /// <summary>Who a live token belongs to (<see cref="Operator"/> for him); null for anything that is not a live token.</summary>
    string? UserOf(string? token);

    /// <summary>Forget a token; true when it was live.</summary>
    bool Destroy(string? token);
}
