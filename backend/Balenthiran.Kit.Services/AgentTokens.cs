using System.Security.Cryptography;
using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-USERS-9's run tokens: 32 random bytes, shown once, kept only as a SHA-256. A token is a bearer
/// secret for one run's model spend, so it is never stored, logged or echoed.
/// </summary>
public static class AgentTokens
{
    /// <summary>A recognisable prefix, so a leaked token is found by a secret scanner and not mistaken for Anthropic's own key.</summary>
    public const string Prefix = "kitrun_";

    /// <summary>A new token: the prefix and 32 random bytes, base64url.</summary>
    public static string Mint() => Prefix + Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>The lower-case hex SHA-256 of a token: all that is stored, and what a model call is found by.</summary>
    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
