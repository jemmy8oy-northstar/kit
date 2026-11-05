namespace Balenthiran.Kit.Services;

/// <summary>A freshly minted installation token for one repo. Printing it must never reveal the token.</summary>
public sealed record GitHubTokenGrant(string Token, DateTimeOffset ExpiresAt)
{
    /// <inheritdoc />
    public override string ToString() => $"GitHub token, expires {ExpiresAt:O}";
}
