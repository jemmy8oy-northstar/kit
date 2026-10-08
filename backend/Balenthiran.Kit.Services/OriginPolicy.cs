using System.Globalization;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Ported from <c>ui.js</c>'s <c>originAllowed()</c>, <c>isLoopback()</c> and
/// <c>crossOriginWrite()</c>.
/// </summary>
/// <param name="publicOrigin"><c>KIT_PUBLIC_ORIGIN</c>, or null.</param>
public sealed class OriginPolicy(IUrlParser urls, string? publicOrigin) : IOriginPolicy
{
    private static readonly Regex Ipv4Loopback = new(@"^127\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})\z", RegexOptions.Compiled);

    /// <summary>
    /// Loopback always; otherwise only the configured public origin, compared as a WHOLE
    /// parsed origin — never a substring, which has a famous bypass in each direction.
    /// </summary>
    public bool Allowed(string origin)
    {
        // A malformed Origin is refused, never treated as "no origin" — that branch allows.
        if (urls.Parse(origin, againstLocalhost: false) is not { } url)
        {
            return false;
        }

        if (IsLoopback(url.Host))
        {
            return true;
        }

        if (string.IsNullOrEmpty(publicOrigin) || urls.Parse(publicOrigin, againstLocalhost: false) is not { } want)
        {
            return false;
        }

        return url.Origin == want.Origin;
    }

    /// <inheritdoc />
    public IApiError? RefuseWrite(string? origin)
    {
        // No Origin at all is a non-browser caller (curl, the CLI) — the normal case, not a hole.
        if (origin is null || Allowed(origin))
        {
            return null;
        }

        return new ApiError
        {
            Error = "cross-origin-write",
            Reason = $"a write carrying Origin '{origin}' came from a page this server does not serve. "
                + "Kit's UI is a local developer tool; a page on another origin editing your working "
                + "tree is CSRF, not a feature.",
        };
    }

    /// <summary><c>ui.js</c>'s <c>isLoopback</c>, on a serialised hostname.</summary>
    public static bool IsLoopback(string host)
    {
        if (host is "localhost" or "::1" or "[::1]")
        {
            return true;
        }

        var m = Ipv4Loopback.Match(host);
        return m.Success && Enumerable.Range(1, 3).All(g => int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture) <= 255);
    }
}
