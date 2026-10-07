using System.Text.Json;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Ported from <c>ui.js</c>'s <c>answer()</c>, <c>delivered()</c>, <c>cors()</c> and
/// <c>originAllowed()</c>: the raw target through WHATWG parsing, the base-path strip
/// (rule 8), the preflight, and who may read a reply. Scored by
/// <c>conformance/routes/host.json</c>.
///
/// ⚠️ CORS governs who may READ a reply. It never stopped a cross-origin write — that is
/// the Origin check the write half carries, which is not ported yet.
/// </summary>
/// <param name="basePath">Already normalised (<see cref="NormaliseBasePath"/>).</param>
/// <param name="publicOrigin"><c>KIT_PUBLIC_ORIGIN</c>, or null.</param>
public sealed class KitHost(IKitRouter router, IUrlParser urls, IEngineJsonSerialiser serialiser, string basePath, string? publicOrigin) : IKitHost
{
    private static readonly Regex Ipv4Loopback = new(@"^127\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})\z", RegexOptions.Compiled);

    // `JSON.stringify(body)`: the engine's settings, unindented.
    private readonly JsonSerializerOptions json = new(serialiser.Options) { WriteIndented = false };

    /// <summary>
    /// Rule 8's one spelling of the prefix: a leading slash and no trailing one, or
    /// <c>""</c> for the root. <c>kit</c>, <c>/kit</c>, <c>/kit/</c> all mean <c>/kit</c>.
    /// </summary>
    public static string NormaliseBasePath(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim(CorpusParser.WsChars).TrimStart('/').TrimEnd('/');
        return trimmed.Length == 0 ? string.Empty : "/" + trimmed;
    }

    /// <inheritdoc />
    public IHostAnswer Answer(string method, string target, string? origin)
    {
        // `new URL` throws on a target such as `//x:99999/`; in ui.js that once crashed
        // the process. Here, as there now, it is the client's error.
        if (urls.Parse(target, againstLocalhost: true) is not { } url)
        {
            return Deliver(Json(400, new ApiError { Error = "bad-request", Reason = "the request target is not a valid URL" }), origin);
        }

        var requested = url.Pathname;

        // Before everything else: a request outside the prefix belongs to a sibling app.
        if (StripBasePath(requested) is not { } pathname)
        {
            return Deliver(Json(404, new ApiError { Error = "no-such-route", Reason = $"this Kit is served under {basePath} — nothing is served at {requested}" }), origin);
        }

        // A preflight is answered by the same allowlist as the request, so the two cannot disagree.
        if (method == "OPTIONS")
        {
            return new HostAnswer { Status = 204, Headers = Cors(origin), Body = string.Empty };
        }

        if (method == "POST")
        {
            return new HostAnswer { Post = pathname };
        }

        return Deliver(router.Route(method, pathname), origin);
    }

    /// <inheritdoc />
    public IHostAnswer Deliver(IKitResponse response, string? origin)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["content-type"] = response.ContentType };
        foreach (var (k, v) in Cors(origin))
        {
            headers[k] = v;
        }

        if (!string.IsNullOrEmpty(response.CacheControl))
        {
            headers["cache-control"] = response.CacheControl;
        }

        return response.Raw is not null
            ? new HostAnswer { Status = response.Status, Headers = headers, Raw = response.Raw }
            : new HostAnswer { Status = response.Status, Headers = headers, Body = JsonSerializer.Serialize(response.Body, response.Body!.GetType(), json) };
    }

    /// <summary>
    /// May a page at <paramref name="origin"/> read this Kit's replies? Loopback always;
    /// otherwise only the configured public origin, compared as a WHOLE parsed origin —
    /// never a substring, which has a famous bypass in each direction.
    /// </summary>
    public bool OriginAllowed(string origin)
    {
        // A malformed Origin is refused, never treated as "no origin".
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

    /// <summary><c>ui.js</c>'s <c>isLoopback</c>, on a serialised hostname.</summary>
    public static bool IsLoopback(string host)
    {
        if (host is "localhost" or "::1" or "[::1]")
        {
            return true;
        }

        var m = Ipv4Loopback.Match(host);
        return m.Success && Enumerable.Range(1, 3).All(g => int.Parse(m.Groups[g].Value, System.Globalization.CultureInfo.InvariantCulture) <= 255);
    }

    private Dictionary<string, string> Cors(string? origin)
    {
        if (string.IsNullOrEmpty(origin) || !OriginAllowed(origin))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["access-control-allow-origin"] = origin,
            ["access-control-allow-methods"] = "GET, POST, OPTIONS",
            ["access-control-allow-headers"] = "content-type",

            // Two origins get two answers; a cache that forgets that serves one the other's.
            ["vary"] = "Origin",
        };
    }

    /// <summary>The path as the app sees it, or null for "not under our prefix at all". Never <c>StartsWith(basePath)</c>: that accepts <c>/kitten</c>.</summary>
    private string? StripBasePath(string pathname)
    {
        if (basePath.Length == 0)
        {
            return pathname;
        }

        if (pathname == basePath)
        {
            return "/";
        }

        return pathname.StartsWith(basePath + "/", StringComparison.Ordinal) ? pathname[basePath.Length..] : null;
    }

    private static KitResponse Json(int status, object body) => new() { Status = status, ContentType = "application/json", Body = body };
}
