namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// The live session tokens (<c>auth.js</c>'s <c>sessions()</c>). A server with a password uses
/// <c>SignedSessionStore</c>, whose tokens survive a restart without a session table (the
/// database he parked on kit#41); the in-memory <c>SessionStore</c> is the oracle's.
/// </summary>
public interface ISessionStore
{
    /// <summary>Mint a token that is valid for the TTL.</summary>
    string Create();

    /// <summary>Is this a token the store minted and has not yet expired or destroyed?</summary>
    bool Valid(string? token);

    /// <summary>Forget a token; true when it was live.</summary>
    bool Destroy(string? token);
}
