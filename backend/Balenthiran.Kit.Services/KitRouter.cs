using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Database;

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
/// route, the app, the body — then the edit, and git write-back when it is switched on.
/// </summary>
/// <param name="password"><c>KIT_PASSWORD</c>, or null.</param>
/// <param name="host">The address the server is bound to: with no password, writes are served only on loopback.</param>
/// <param name="secure">Set the cookie's <c>Secure</c> flag (an https public origin).</param>
/// <param name="git">Write-back (<c>--git</c>); off when not given, as <c>ui.js</c> without the flag.</param>
/// <param name="deployed">A public origin is configured (<c>KIT_PUBLIC_ORIGIN</c>): with write-back off, a write says the edit is on the server's disk only.</param>
/// <param name="pulls">Commit's door to <c>dev</c> (kit#155): proposes the edits branch as a pull request. None means Commit says so.</param>
/// <param name="head">The branch every edit is pushed to (<c>KIT_GIT_BRANCH</c>, <c>kit/hosted</c> when deployed) — Commit's pull request is FROM it.</param>
/// <param name="baseBranch">What Commit proposes the edits INTO: <c>KIT_GIT_BASE</c>, default <c>dev</c>, the branch the entrypoint starts <paramref name="head"/> from.</param>
public sealed class KitRouter(
    ICorpusDirectory corpora,
    IProjectViewer viewer,
    IUiBundle bundle,
    string? password,
    IOriginPolicy? origins = null,
    ISessionStore? sessions = null,
    ISignInThrottle? throttle = null,
    string host = "127.0.0.1",
    bool secure = false,
    ICorpusWriter? writer = null,
    IGitStore? git = null,
    bool deployed = false,
    IPullRequestOpener? pulls = null,
    string? head = null,
    string baseBranch = "dev") : IKitRouter
{
    private const string Cookie = "kit_session";

    // `auth.enabled()`: a password that is more than JavaScript whitespace.
    private readonly bool lockRequired = password is not null && password.Trim(CorpusParser.WsChars).Length > 0;

    private readonly IOriginPolicy origins = origins ?? new OriginPolicy(new UrlParser(), null);

    private readonly ICorpusWriter writer = writer ?? new CorpusWriter(new CorpusParser());

    private readonly IGitStore git = git ?? new GitStore(enabled: false);

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

        // Below the lock like every edit: proposing the edits to `dev` is a write in its own right.
        if (pathname == "/api/commit")
        {
            return Commit();
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

        var b = body.Value;
        return bm.Success ? Bind(app, b) : Edit(app, m, b);
    }

    /// <summary><c>write()</c> past its gates: create a behaviour, add a step, or adjudicate.</summary>
    private KitResponse Edit(string app, Match m, JsonElement body)
    {
        var review = m.Groups[3].Value == "review";
        var step = m.Groups[2].Success && !review;

        // Decoded with a refusal, never a throw — in ui.js this once crashed the process.
        string? id;
        if (m.Groups[2].Success)
        {
            if (DecodeUriComponent(m.Groups[2].Value) is not { } decoded)
            {
                return Json(400, new ApiError { Error = "bad-request", Reason = "the behaviour id is not valid percent-encoding" });
            }

            id = decoded;
        }
        else
        {
            id = Field(body, "id") is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;
        }

        if (TypeError(body, review ? ReviewFields : step ? StepFields : CreateFields) is { } wrongType)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = wrongType });
        }

        var text = corpora.ReadText(app);
        var result = review
            ? writer.SetReview(text, id!, Str(body, "state")!, Str(body, "note"))
            : step
                ? writer.AddStep(text, id!, Str(body, "step")!)
                : writer.AddBehaviour(text, id!, Str(body, "title")!, Str(body, "actor"), Field(body, "steps") is { ValueKind: JsonValueKind.Array } s ? s.EnumerateArray().Select(x => x.GetString()!).ToList() : null, Str(body, "source"), Str(body, "ref"));

        // 409, not 500: every refusal is a statement about the request.
        if (!result.Ok)
        {
            return Json(409, new ApiError { Error = result.Error!, Reason = result.Reason!, Known = result.Known?.ToList() });
        }

        corpora.WriteText(app, result.Text!);
        var what = review ? $"adjudicate {id}" : step ? $"add a step to {id}" : $"add {id}";
        return Json(200, GitOutcome(corpora.FullPath(app), what, app, new WriteOutcome { App = app, Behaviour = id, File = corpora.RelativePath(app), Note = NotCommitted }));
    }

    /// <summary>
    /// Commit (kit#155 slice 2): propose every edit on the edits branch to <c>dev</c> as ONE pull
    /// request, for him or CI to merge. Nothing here touches the corpus or git — each edit was
    /// already pushed when it was made, so Commit only asks GitHub. A second press finds the
    /// open pull request rather than opening another. The body is not read: there is nothing
    /// to choose, because there is one edits branch and one base.
    ///
    /// Every refusal is 409 with the opener's own sentence — GitHub's words where GitHub spoke —
    /// so the UI shows one shape and the reason names the layer.
    /// </summary>
    private KitResponse Commit()
    {
        if (!git.Enabled)
        {
            return Json(409, new ApiError
            {
                Error = "git-off",
                Reason = "git write-back is off, so no edit has been pushed anywhere and there is nothing to propose to " + baseBranch,
            });
        }

        if (string.IsNullOrEmpty(head))
        {
            return Json(409, new ApiError { Error = "no-edits-branch", Reason = "Kit was not told which branch its edits are pushed to (KIT_GIT_BRANCH), so it cannot propose them" });
        }

        if (pulls is null)
        {
            return Json(409, new ApiError { Error = "no-pull-request", Reason = "this Kit has no way to open a pull request" });
        }

        // Sync over async, as GitStore runs git synchronously: the host has no synchronisation
        // context to deadlock on. SERIALISED, because Kestrel is not: two presses at once would
        // both find no open pull request and both create one, and the second would come back as
        // GitHub's 422 instead of "already open" (kit#160's blind review). Behind the lock the
        // second press finds the first one's pull request.
        IPullRequestResult r;
        lock (commitGate)
        {
            r = pulls.OpenAsync(head, baseBranch, $"kit: hosted edits ({head} → {baseBranch})", CommitBody).GetAwaiter().GetResult();
        }
        return r.Reason is { } reason
            ? Json(409, new ApiError { Error = "no-pull-request", Reason = reason })
            : Json(200, r);
    }

    private readonly object commitGate = new();

    private const string CommitBody ="Edits made in the hosted Kit. Each one was committed and pushed as it was made; merging this lands them on the base branch.";

    /// <summary><c>postBinding()</c> past its gates: add one binding to the app's bindings file.</summary>
    private KitResponse Bind(string app, JsonElement body)
    {
        if (TypeError(body, BindFields) is { } wrongType)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = wrongType });
        }

        // No bindings file yet is the NORMAL first bind, not an error.
        var before = corpora.ReadBindingsText(app) ?? "{}";
        var skipped = new List<string>();
        var nouns = writer.CorpusNouns(corpora.Corpora().ToDictionary(a => a, corpora.ReadText, StringComparer.Ordinal), skipped);
        var value = Field(body, "binding") ?? JsonSerializer.SerializeToElement<object?>(null);
        var result = writer.AddBinding(before, Str(body, "noun")!, value, nouns, app);
        if (!result.Ok)
        {
            return Json(409, new ApiError { Error = result.Error!, Reason = result.Reason!, Current = result.Current });
        }

        corpora.WriteBindingsText(app, result.Text!);
        return Json(200, GitOutcome(corpora.FullBindingsPath(app), $"bind {result.Noun}", app, new WriteOutcome
        {
            App = app,
            Noun = result.Noun,
            File = corpora.RelativeBindingsPath(app),
            Note = NotCommitted,
            SharedWith = result.SharedWith!.ToList(),
            UnreadableCorpora = skipped,
        }));
    }

    /// <summary>
    /// <c>gitOutcome()</c>: what git did with this edit, as fields a caller can act on — one
    /// helper for both write paths, so they cannot describe the same outcome two ways. Off,
    /// the answer is <paramref name="plain"/> unchanged (decision 2) — except deployed, where
    /// "commit it yourself" would name a working tree inside a pod nobody can reach (kit#117),
    /// so the note says where the edit really is and a warning says it. On, it becomes a
    /// <see cref="GitWriteOutcome"/>, whose <c>note</c> is git's own reason whenever the edit
    /// did not reach the remote, never a summary of it.
    /// </summary>
    private WriteOutcome GitOutcome(string file, string summary, string app, WriteOutcome plain)
    {
        var g = git.WriteBack(file, summary, app);
        if (!git.Enabled)
        {
            return deployed
                ? new WriteOutcome
                {
                    App = plain.App,
                    Behaviour = plain.Behaviour,
                    Noun = plain.Noun,
                    File = plain.File,
                    SharedWith = plain.SharedWith,
                    UnreadableCorpora = plain.UnreadableCorpora,
                    Note = DeployedNote,
                    Warning = DeployedWarning,
                }
                : plain;
        }

        return new GitWriteOutcome
        {
            App = plain.App,
            Behaviour = plain.Behaviour,
            Noun = plain.Noun,
            File = plain.File,
            SharedWith = plain.SharedWith,
            UnreadableCorpora = plain.UnreadableCorpora,
            Committed = g.Committed,
            Pushed = g.Pushed,
            Commit = g.Commit,
            Branch = g.Branch,
            Note = g.Pushed ? $"committed as {g.Commit} and pushed to {g.Branch}." : g.Reason!,
            Warning = g.Committed && !g.Pushed ? $"this edit is committed locally but did NOT reach {git.Remote}: {g.Reason}" : null,
        };
    }

    // Decision 2: with write-back off, the answer says what was NOT done.
    private const string NotCommitted = "written to the working tree. Kit does not run git — review the diff and commit it yourself.";

    private const string DeployedNote = "written to this server's disk only. Git write-back is off, so the edit has reached no repository.";

    private const string DeployedWarning = "Git write-back is off on this deployed Kit.";

    // ui.js's bodyTypeError specs: `string` is required; `string?` may be null or absent;
    // `strings?` is an array of strings, null or absent.
    private static readonly (string Field, string Type)[] CreateFields = [("id", "string"), ("title", "string"), ("actor", "string?"), ("steps", "strings?"), ("source", "string?"), ("ref", "string?")];
    private static readonly (string Field, string Type)[] StepFields = [("step", "string")];
    private static readonly (string Field, string Type)[] ReviewFields = [("state", "string"), ("note", "string?")];
    private static readonly (string Field, string Type)[] BindFields = [("noun", "string")];

    private static string? TypeError(JsonElement body, (string Field, string Type)[] spec)
    {
        foreach (var (field, type) in spec)
        {
            var v = Field(body, field);
            var absent = v is null || v.Value.ValueKind == JsonValueKind.Null;
            var isString = v is { ValueKind: JsonValueKind.String };
            if (type == "string" && !isString)
            {
                return $"{field} must be a string";
            }

            if (type == "string?" && !absent && !isString)
            {
                return $"{field} must be a string";
            }

            if (type == "strings?" && !absent && !(v!.Value.ValueKind == JsonValueKind.Array && v.Value.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String)))
            {
                return $"{field} must be a list of strings";
            }
        }

        return null;
    }

    /// <summary><c>body[field]</c>: an own property of an object (last duplicate wins); an array has none.</summary>
    private static JsonElement? Field(JsonElement body, string field) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty(field, out var v) ? v : null;

    private static string? Str(JsonElement body, string field) =>
        Field(body, field) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

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
