using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>Ported from <c>auth.js</c>'s <c>throttle()</c>.</summary>
/// <param name="now">Milliseconds since the epoch; injectable so the cooldown is testable.</param>
public sealed class SignInThrottle(Func<long>? now = null) : ISignInThrottle
{
    /// <summary>That many attempts are free and the NEXT one is refused.</summary>
    public const int FreeAttempts = 5;

    public const long CooldownBaseMs = 10 * 1000;

    public const long CooldownMaxMs = 15 * 60 * 1000;

    private readonly Func<long> now = now ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    private readonly Lock gate = new();
    private int failures;
    private long blockedUntil;

    /// <inheritdoc />
    public long RetryAfterMs()
    {
        lock (gate)
        {
            var left = blockedUntil - now();
            return left > 0 ? left : 0;
        }
    }

    /// <inheritdoc />
    public void Fail()
    {
        lock (gate)
        {
            failures++;
            if (failures >= FreeAttempts)
            {
                // Doubling, capped: uncapped, it overflows and locks the owner out for good.
                var over = failures - FreeAttempts + 1;
                var wait = over > 20 ? CooldownMaxMs : Math.Min(CooldownBaseMs * (1L << (over - 1)), CooldownMaxMs);
                blockedUntil = now() + wait;
            }
        }
    }

    /// <inheritdoc />
    public void Succeed()
    {
        lock (gate)
        {
            failures = 0;
            blockedUntil = 0;
        }
    }
}
