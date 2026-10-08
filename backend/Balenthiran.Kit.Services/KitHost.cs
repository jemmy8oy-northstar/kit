using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Ported from <c>ui.js</c>'s <c>answer()</c>, <c>received()</c>, <c>delivered()</c> and
/// <c>cors()</c>: the raw target through WHATWG parsing, the base-path strip (rule 8), the
/// preflight, the CSRF refusal before a body is read, the body's size and JSON, and who
/// may read a reply. Scored by <c>conformance/routes/host.json</c> and <c>auth.json</c>.
/// </summary>
/// <param name="basePath">Already normalised (<see cref="NormaliseBasePath"/>).</param>
public sealed class KitHost(IKitRouter router, IUrlParser urls, IEngineJsonSerialiser serialiser, IOriginPolicy origins, string basePath) : IKitHost
{
    /// <summary>A request body over this many bytes is not a behaviour.</summary>
    public const int MaxBody = 64 * 1024;

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
    public IHostAnswer Answer(string method, string target, string? origin, string? cookie)
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

        if (method != "POST")
        {
            return Deliver(router.Route(method, pathname, cookie: cookie), origin);
        }

        // Refused as cross-origin BEFORE the body is read — not as bad JSON or too large.
        if (origins.RefuseWrite(origin) is { } refused)
        {
            return Deliver(Json(403, refused), origin);
        }

        return new HostAnswer { Post = pathname };
    }

    /// <inheritdoc />
    public IHostAnswer Received(string pathname, byte[]? raw, string? origin, string? cookie)
    {
        if (raw is null)
        {
            return Deliver(Json(413, new ApiError { Error = "too-large", Reason = $"a request body over {MaxBody} bytes is not a behaviour" }), origin);
        }

        // `JSON.parse(buf.toString('utf8') || 'null')`: decoded with replacement first, as
        // Node's toString does; a BOM stays and is refused; duplicate keys are last-wins;
        // depth is bounded only by the body limit, as V8's is in practice.
        var text = Encoding.UTF8.GetString(raw);
        JsonDocument body;
        try
        {
            body = JsonDocument.Parse(text.Length == 0 ? "null" : text, new JsonDocumentOptions { MaxDepth = MaxBody });
        }
        catch (JsonException)
        {
            return Deliver(Json(400, new ApiError { Error = "bad-json", Reason = "the request body is not valid JSON" }), origin);
        }

        using (body)
        {
            return Deliver(router.Route("POST", pathname, cookie, origin, body.RootElement), origin);
        }
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

        if (!string.IsNullOrEmpty(response.SetCookie))
        {
            headers["set-cookie"] = response.SetCookie;
        }

        return response.Raw is not null
            ? new HostAnswer { Status = response.Status, Headers = headers, Raw = response.Raw }
            : new HostAnswer { Status = response.Status, Headers = headers, Body = JsonSerializer.Serialize(response.Body, response.Body!.GetType(), json) };
    }

    private Dictionary<string, string> Cors(string? origin)
    {
        if (string.IsNullOrEmpty(origin) || !origins.Allowed(origin))
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
