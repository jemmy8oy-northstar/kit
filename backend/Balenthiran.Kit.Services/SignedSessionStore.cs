using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// A session token that survives a restart (BEH-GATE-H3): the token carries its own expiry and
/// an HMAC over it, so validating one needs the key and nothing remembered. The in-memory
/// <see cref="SessionStore"/> signed him out on every deploy; this does it without the session
/// table kit#41 parked.
/// </summary>
/// <remarks>
/// The key is derived from the password, so changing the password signs every session out, and
/// a new secret is not needed. Sign-out is remembered in memory only: a token signed out before
/// a restart is valid again after it, until its expiry. The browser has already dropped it.
/// </remarks>
/// <param name="key">The HMAC key; see <see cref="KeyFor"/>.</param>
/// <param name="now">Milliseconds since the epoch; injectable so expiry is testable.</param>
public sealed class SignedSessionStore(byte[] key, Func<long>? now = null) : ISessionStore
{
    private readonly Func<long> now = now ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    private readonly Dictionary<string, long> revoked = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>
    /// PBKDF2 over the password, so a stolen cookie is not a cheap offline test of password
    /// guesses. Run once at startup.
    /// </summary>
    public static byte[] KeyFor(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(password, Encoding.UTF8.GetBytes("kit-session-v1"), 210_000, HashAlgorithmName.SHA256, 32);

    /// <inheritdoc />
    public string Create()
    {
        var expires = now() + SessionStore.TtlMs;
        var body = $"{expires.ToString(CultureInfo.InvariantCulture)}.{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}";
        return $"{body}.{Sign(body)}";
    }

    /// <inheritdoc />
    public bool Valid(string? token) => Expiry(token) is { } expires && expires > now() && !IsRevoked(token!);

    /// <inheritdoc />
    public bool Destroy(string? token)
    {
        if (Expiry(token) is not { } expires || expires <= now())
        {
            return false;
        }

        lock (gate)
        {
            Sweep();
            return revoked.TryAdd(token!, expires);
        }
    }

    /// <summary>The expiry a well-formed, correctly signed token claims; null for anything else.</summary>
    private long? Expiry(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var cut = token.LastIndexOf('.');
        if (cut <= 0)
        {
            return null;
        }

        var body = token[..cut];
        var given = Encoding.ASCII.GetBytes(token[(cut + 1)..]);
        if (!CryptographicOperations.FixedTimeEquals(given, Encoding.ASCII.GetBytes(Sign(body))))
        {
            return null;
        }

        var dot = body.IndexOf('.');
        return dot > 0 && long.TryParse(body[..dot], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ? expires : null;
    }

    private bool IsRevoked(string token)
    {
        lock (gate)
        {
            return revoked.ContainsKey(token);
        }
    }

    private string Sign(string body) => Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(body)));

    /// <summary>A revoked token past its expiry is refused anyway, so it need not be remembered.</summary>
    private void Sweep()
    {
        var t = now();
        foreach (var token in revoked.Where(kv => kv.Value <= t).Select(kv => kv.Key).ToList())
        {
            revoked.Remove(token);
        }
    }
}
