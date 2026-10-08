namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Refuses sign-in after repeated failures (<c>auth.js</c>'s <c>throttle()</c>). GLOBAL, not
/// per caller: behind the ingress every request has the proxy's address, and
/// <c>X-Forwarded-For</c> is a header the guesser writes.
/// </summary>
public interface ISignInThrottle
{
    /// <summary>Milliseconds before another attempt is allowed; 0 means go ahead.</summary>
    long RetryAfterMs();

    void Fail();

    /// <summary>A correct password clears the record.</summary>
    void Succeed();
}
