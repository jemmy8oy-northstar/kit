using System.Text;
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
/// ⚠️ Not yet ported, and answered as such rather than guessed: every write
/// (<c>POST</c>) — a later PR of the #119 stack.
/// </summary>
/// <param name="password"><c>KIT_PASSWORD</c>, or null.</param>
public sealed class KitRouter(ICorpusDirectory corpora, IProjectViewer viewer, IUiBundle bundle, string? password) : IKitRouter
{
    // `auth.enabled()`: a password that is more than JavaScript whitespace.
    private readonly bool lockRequired = password is not null && password.Trim(CorpusParser.WsChars).Length > 0;

    private static readonly Regex Api = new(@"^/api(/|\z)", RegexOptions.Compiled);
    private static readonly Regex OneProject = new(@"^/api/projects/([^/]+)\z", RegexOptions.Compiled);

    /// <inheritdoc />
    public IKitResponse Route(string method, string pathname)
    {
        if (method == "POST")
        {
            return Json(501, new ApiError { Error = "not-implemented", Reason = "writes are not ported to the C# server yet (#119)" });
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

        // Readable without a session: it reveals only whether a lock exists. Nobody
        // can be signed in until the write half (sign-in) is ported.
        if (pathname == "/api/session")
        {
            return Json(200, new SessionState { Required = lockRequired, SignedIn = !lockRequired });
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
