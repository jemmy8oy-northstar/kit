namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// The live session tokens (<c>auth.js</c>'s <c>sessions()</c>): in memory, so a restart
/// signs everyone out — deliberately, because a session table would be the database he
/// parked on kit#41 arriving as an implementation detail.
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
