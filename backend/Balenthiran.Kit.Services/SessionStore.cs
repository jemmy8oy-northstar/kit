using System.Security.Cryptography;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>Ported from <c>auth.js</c>'s <c>sessions()</c>.</summary>
/// <param name="now">Milliseconds since the epoch; injectable so expiry is testable.</param>
/// <param name="mint">Token source; the default is 32 bytes from the CSPRNG, hex. Injectable for the oracle only.</param>
public sealed class SessionStore(Func<long>? now = null, Func<string>? mint = null) : ISessionStore
{
    /// <summary>Seven days: the client is his phone, where signing in repeatedly decides whether he uses it.</summary>
    public const long TtlMs = 7L * 24 * 60 * 60 * 1000;

    private readonly Func<long> now = now ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    private readonly Func<string> mint = mint ?? (() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));
    private readonly Dictionary<string, long> live = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <inheritdoc />
    public string Create()
    {
        lock (gate)
        {
            var token = mint();
            live[token] = now() + TtlMs;
            return token;
        }
    }

    /// <inheritdoc />
    public bool Valid(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        lock (gate)
        {
            Sweep();
            return live.ContainsKey(token);
        }
    }

    /// <inheritdoc />
    public bool Destroy(string? token)
    {
        lock (gate)
        {
            return token is not null && live.Remove(token);
        }
    }

    /// <summary>Drop what has expired — `expires &lt;= now`, so a token dies AT its TTL.</summary>
    private void Sweep()
    {
        var t = now();
        foreach (var token in live.Where(kv => kv.Value <= t).Select(kv => kv.Key).ToList())
        {
            live.Remove(token);
        }
    }
}
