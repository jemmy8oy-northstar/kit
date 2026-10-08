using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Ported from <c>ui.js</c>'s <c>route()</c> — the READ half. Everything under
/// <c>/api</c> answers JSON to the end, its 404 included: falling back to a page
/// would hand a broken fetch HTML and report it as a parse error three layers away.
///
/// Everything else is the built UI (<see cref="IUiBundle"/>).
///
/// Every POST passes <c>write()</c>'s gates in its order — the CSRF Origin check, sign-in
/// and sign-out, the lock (a session when a password is set, else a loopback bind), the
/// route, the app, the body. ⚠️ The write itself is not ported yet and answers 501: a
/// later PR of #119.
/// </summary>
/// <param name="password"><c>KIT_PASSWORD</c>, or null.</param>
/// <param name="host">The address the server is bound to: with no password, writes are served only on loopback.</param>
/// <param name="secure">Set the cookie's <c>Secure</c> flag (an https public origin).</param>
public sealed class KitRouter(
    ICorpusDirectory corpora,
    IProjectViewer viewer,
    IUiBundle bundle,
    string? password,
    IOriginPolicy? origins = null,
    ISessionStore? sessions = null,
    ISignInThrottle? throttle = null,
    string host = "127.0.0.1",
    bool secure = false) : IKitRouter
{
    private const string Cookie = "kit_session";

    // `auth.enabled()`: a password that is more than JavaScript whitespace.
    private readonly bool lockRequired = password is not null && password.Trim(CorpusParser.WsChars).Length > 0;

    private readonly IOriginPolicy origins = origins ?? new OriginPolicy(new UrlParser(), null);

    private static readonly Regex Api = new(@"^/api(/|\z)", RegexOptions.Compiled);
    private static readonly Regex OneProject = new(@"^/api/projects/([^/]+)\z", RegexOptions.Compiled);
    private static readonly Regex Bindings = new(@"^/api/projects/([^/]+)/bindings\z", RegexOptions.Compiled);
    private static readonly Regex Behaviours = new(@"^/api/projects/([^/]+)/behaviours(?:/([^/]+)/(steps|review))?\z", RegexOptions.Compiled);

    /// <inheritdoc />
    public IKitResponse Route(string method, string pathname, string? cookie = null, string? origin = null, JsonElement? body = null)
    {
        if (method == "POST")
        {
            return Write(pathname, cookie, origin, body);
        }

        if (method != "GET")
        {
            return Json(405, new ApiError
            {
                Error = "method-not-allowed",
                Reason = $"this server serves GET and POST; {method} reaches no handler",
                Allow = "GET, POST",
            });
        }

        if (!Api.IsMatch(pathname))
        {
            return bundle.Serve(pathname);
        }

        if (pathname == "/api/health")
        {
            return Json(200, new Health { Ok = true });
        }

        // Readable without a session: it reveals only whether a lock exists.
        if (pathname == "/api/session")
        {
            return Json(200, new SessionState { Required = lockRequired, SignedIn = !lockRequired || SignedIn(cookie) });
        }

        if (pathname == "/api/projects")
        {
            return Json(200, new ProjectList { Projects = corpora.Corpora().Select(viewer.Summary).ToList() });
        }

        var match = OneProject.Match(pathname);
        if (match.Success)
        {
            // Decoded FIRST, so an encoded traversal is compared as the string it
            // decodes to — and then only ever looked up, never joined to a path.
            if (DecodeUriComponent(match.Groups[1].Value) is not { } app)
            {
                return Json(400, new ApiError { Error = "bad-request", Reason = "the app name is not valid percent-encoding" });
            }

            var known = corpora.Corpora();
            if (!known.Contains(app, StringComparer.Ordinal))
            {
                return Json(404, new ApiError { Error = "no-such-project", Reason = $"no corpus named '{app}'", Known = known.ToList() });
            }

            try
            {
                return Json(200, viewer.View(app));
            }
            catch (ProjectionFailedException e)
            {
                // A reason, never a stack: the stack names paths inside the pod.
                return Json(500, new ApiError { Error = "projection-failed", Reason = e.Message });
            }
        }

        return Json(404, new ApiError { Error = "no-such-route", Reason = $"nothing is served at {pathname}" });
    }

    /// <summary><c>ui.js</c>'s <c>write()</c>, up to the write itself.</summary>
    private KitResponse Write(string pathname, string? cookie, string? origin, JsonElement? body)
    {
        // Rule 4 FIRST: the only gate that defends against a caller who is not the developer.
        if (origins.RefuseWrite(origin) is { } refused)
        {
            return Json(403, refused);
        }

        // Below the Origin check (else any page could run a guessing loop through his
        // browser) and above the lock (else the key is locked inside the box it opens).
        if (pathname is "/api/session" or "/api/session/end")
        {
            return Session(pathname, cookie, body);
        }

        // Exclusive on purpose: with a password the session decides and the bind address
        // is irrelevant — there is no "loopback, so allow" while a password is set.
        if (lockRequired)
        {
            if (!SignedIn(cookie))
            {
                return Json(401, new ApiError
                {
                    Error = "not-signed-in",
                    Reason = "this Kit is password-protected and this request carries no valid session. POST the password to /api/session first.",
                });
            }
        }
        else if (!OriginPolicy.IsLoopback(host))
        {
            return Json(403, new ApiError
            {
                Error = "not-loopback",
                Reason = $"writes are served only to loopback; this server is bound to {host}. "
                    + "docs/design/ui.md decision 1: Kit's UI is a local developer tool, and an "
                    + "unauthenticated write API on a routable interface is not that.",
            });
        }

        var bm = Bindings.Match(pathname);
        var m = bm.Success ? bm : Behaviours.Match(pathname);
        if (!m.Success)
        {
            return Json(404, new ApiError { Error = "no-such-route", Reason = $"nothing accepts a POST at {pathname}" });
        }

        if (DecodeUriComponent(m.Groups[1].Value) is not { } app)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "the app name is not valid percent-encoding" });
        }

        // Rule 3: looked UP in the listing, never joined to a path.
        var known = corpora.Corpora();
        if (!known.Contains(app, StringComparer.Ordinal))
        {
            return Json(404, new ApiError { Error = "no-such-project", Reason = $"no corpus named '{app}'", Known = known.ToList() });
        }

        // `!body || typeof body !== 'object'`: an object or an array; null and scalars are not.
        if (body is not { ValueKind: JsonValueKind.Object or JsonValueKind.Array })
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "the body must be a JSON object" });
        }

        return Json(501, new ApiError { Error = "not-implemented", Reason = "the write itself is not ported to the C# server yet (#119)" });
    }

    /// <summary><c>ui.js</c>'s <c>session()</c>: sign in, sign out.</summary>
    private KitResponse Session(string pathname, string? cookie, JsonElement? body)
    {
        // A Kit with no password has no such route — the page learns that from GET /api/session.
        if (!lockRequired)
        {
            return Json(404, new ApiError { Error = "no-such-route", Reason = "this Kit has no password configured, so there is nothing to sign in to" });
        }

        if (pathname == "/api/session/end")
        {
            sessions?.Destroy(ParseCookies(cookie).GetValueOrDefault(Cookie));

            // Cleared even if the token was already unknown, or the page believes it is
            // signed in and every write 401s with no way back to the form.
            return new KitResponse { Status = 200, ContentType = "application/json", Body = new SignInState { Ok = true, SignedIn = false }, SetCookie = ClearCookie() };
        }

        var wait = throttle?.RetryAfterMs() ?? 0;
        if (wait > 0)
        {
            var seconds = (int)Math.Ceiling(wait / 1000.0);
            return Json(429, new ApiError { Error = "too-many-attempts", Reason = $"too many failed sign-ins; try again in {seconds}s", RetryAfterSeconds = seconds });
        }

        // `typeof given === 'string' ? given : ''` — a missing, numeric or array password is ''.
        var given = body is { ValueKind: JsonValueKind.Object } b && b.TryGetProperty("password", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()!
            : string.Empty;
        if (!SecretsMatch(given, password!))
        {
            throttle?.Fail();

            // One message for missing and wrong: telling them apart helps a guesser.
            return Json(401, new ApiError { Error = "bad-password", Reason = "that is not the password" });
        }

        throttle?.Succeed();
        var token = sessions!.Create();

        // The token goes out ONLY as an HttpOnly cookie, never in the body.
        return new KitResponse { Status = 200, ContentType = "application/json", Body = new SignInState { Ok = true, SignedIn = true }, SetCookie = CookieFor(token) };
    }

    /// <summary>Fails closed: no store means nobody is signed in.</summary>
    private bool SignedIn(string? cookie) => sessions is not null && sessions.Valid(ParseCookies(cookie).GetValueOrDefault(Cookie));

    private string CookieFor(string token) =>
        $"{Cookie}={token}; HttpOnly; SameSite=Strict; Path=/; Max-Age={SessionStore.TtlMs / 1000}" + (secure ? "; Secure" : string.Empty);

    private string ClearCookie() => $"{Cookie}=; HttpOnly; SameSite=Strict; Path=/; Max-Age=0" + (secure ? "; Secure" : string.Empty);

    /// <summary>
    /// <c>auth.js</c>'s <c>parseCookies</c>: tolerant, never throws; a value may contain
    /// <c>=</c> (a base64 token ends in one), and a later duplicate wins.
    /// </summary>
    private static Dictionary<string, string> ParseCookies(string? header)
    {
        var output = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(header))
        {
            return output;
        }

        foreach (var part in header.Split(';'))
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            if (eq < 1)
            {
                continue;
            }

            var name = part[..eq].Trim(CorpusParser.WsChars);
            if (name.Length == 0)
            {
                continue;
            }

            output[name] = part[(eq + 1)..].Trim(CorpusParser.WsChars);
        }

        return output;
    }

    /// <summary>Constant-time over inputs of any length: both sides hashed to 32 bytes first.</summary>
    private static bool SecretsMatch(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(a)), SHA256.HashData(Encoding.UTF8.GetBytes(b)));

    /// <summary>
    /// JavaScript's <c>decodeURIComponent</c>, or null where it would throw a URIError:
    /// a <c>%</c> not followed by two hex digits, or escapes that are not well-formed
    /// UTF-8. .NET's <c>Uri.UnescapeDataString</c> instead leaves both as they are,
    /// which would turn a 400 into a lookup of the raw text.
    /// </summary>
    public static string? DecodeUriComponent(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length;)
        {
            if (s[i] != '%')
            {
                sb.Append(s[i++]);
                continue;
            }

            var bytes = new List<byte>();
            if (Hex(s, i) is not { } lead)
            {
                return null;
            }

            bytes.Add(lead);
            i += 3;

            // How many continuation bytes the lead promises, by the UTF-8 rules
            // ECMA-262's Decode follows (no overlongs, no surrogates, max U+10FFFF).
            var n = lead < 0x80 ? 0 : (lead & 0xE0) == 0xC0 ? 1 : (lead & 0xF0) == 0xE0 ? 2 : (lead & 0xF8) == 0xF0 ? 3 : -1;
            if (n < 0)
            {
                return null;
            }

            for (var k = 0; k < n; k++)
            {
                if (i >= s.Length || s[i] != '%' || Hex(s, i) is not { } cont || (cont & 0xC0) != 0x80)
                {
                    return null;
                }

                bytes.Add(cont);
                i += 3;
            }

            try
            {
                sb.Append(new UTF8Encoding(false, true).GetString(bytes.ToArray()));
            }
            catch (DecoderFallbackException)
            {
                return null;
            }
        }

        return sb.ToString();
    }

    private static byte? Hex(string s, int at) =>
        at + 2 < s.Length && Uri.IsHexDigit(s[at + 1]) && Uri.IsHexDigit(s[at + 2])
            ? Convert.ToByte(s.Substring(at + 1, 2), 16)
            : null;

    private static KitResponse Json(int status, object body) => new() { Status = status, ContentType = "application/json", Body = body };
}
