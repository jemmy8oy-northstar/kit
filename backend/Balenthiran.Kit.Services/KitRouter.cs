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
/// <param name="offReason">Why the entrypoint left write-back off (<c>KIT_GIT_OFF_REASON</c>, kit#233), appended to the deployed warning.</param>
/// <param name="pulls">Commit's door to <c>dev</c> (kit#155): proposes the edits branch as a pull request. None means Commit says so.</param>
/// <param name="head">The branch every edit is pushed to (<c>KIT_GIT_BRANCH</c>, <c>kit/hosted</c> when deployed) — Commit's pull request is FROM it.</param>
/// <param name="baseBranch">What Commit proposes the edits INTO: <c>KIT_GIT_BASE</c>, default <c>dev</c>, the branch the entrypoint starts <paramref name="head"/> from.</param>
/// <param name="pending">Reads what <paramref name="head"/> holds that <paramref name="baseBranch"/> lacks (BEH-COMMIT-5). None means nothing is listed.</param>
/// <param name="users">Early-access accounts (BEH-USERS-1). None means accounts are off: the operator's password is the only way in, as before.</param>
/// <param name="operatorName">The user name the operator signs in with when accounts are on; his password stays <c>KIT_PASSWORD</c>.</param>
/// <param name="github">How a user connects their GitHub (BEH-USERS-1). None means they cannot yet: the session offers no install link.</param>
/// <param name="notifications">The agent's feed (BEH-NOTIFY-1). None means none is configured, and the page says so.</param>
/// <param name="pushStore">Where phones' push subscriptions are kept (BEH-NOTIFY-3). None, or no <paramref name="pushPublicKey"/>, means push is not switched on.</param>
/// <param name="pushPublicKey">The VAPID public key from the optional <c>kit-push</c> secret, which the browser subscribes with.</param>
/// <param name="userProjects">Reads a user's projects from their own GitHub (BEH-USERS-2). None means a user's list stays empty.</param>
/// <param name="userMemory">What Kit remembers about each user (BEH-USERS-4). None means it is kept in memory only.</param>
public sealed partial class KitRouter(
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
    string baseBranch = "dev",
    TimeProvider? clock = null,
    IPendingEditsReader? pending = null,
    ICompletionReader? completions = null,
    string? offReason = null,
    IActivityReader? activity = null,
    IUserStore? users = null,
    string operatorName = KitRouter.DefaultOperator,
    IGitHubConnect? github = null,
    INotificationReader? notifications = null,
    IPushStore? pushStore = null,
    string? pushPublicKey = null,
    IUserProjects? userProjects = null,
    IUserMemoryStore? userMemory = null) : IKitRouter
{
    private const string Cookie = "kit_session";

    // `auth.enabled()`: a password that is more than JavaScript whitespace.
    private readonly bool lockRequired = password is not null && password.Trim(CorpusParser.WsChars).Length > 0;

    private readonly IOriginPolicy origins = origins ?? new OriginPolicy(new UrlParser(), null);

    private readonly ICorpusWriter writer = writer ?? new CorpusWriter(new CorpusParser());

    private readonly IGitStore git = git ?? new GitStore(enabled: false);

    private readonly ICompletionReader completions = completions ?? new CompletionReader(corpora, new GitCorpusHistory());

    private readonly IActivityReader activity = activity ?? new ActivityReader(corpora, new GitCorpusHistory());

    private readonly INotificationReader notifications = notifications ?? new NotificationReader(null, null);

    private readonly IUserMemoryStore userMemory = userMemory ?? new MemoryUserMemoryStore();

    private static readonly Regex Api = new(@"^/api(/|\z)", RegexOptions.Compiled);
    private static readonly Regex OneProject = new(@"^/api/projects/([^/]+)\z", RegexOptions.Compiled);
    private static readonly Regex Bindings = new(@"^/api/projects/([^/]+)/bindings\z", RegexOptions.Compiled);
    private static readonly Regex NotesRoute = new(@"^/api/projects/([^/]+)/notes\z", RegexOptions.Compiled);
    private static readonly Regex PendingRoute = new(@"^/api/projects/([^/]+)/pending\z", RegexOptions.Compiled);
    private static readonly Regex HistoryRoute = new(@"^/api/projects/([^/]+)/history\z", RegexOptions.Compiled);
    private static readonly Regex NoteAction = new(@"^/api/projects/([^/]+)/notes/(reply|dismiss)\z", RegexOptions.Compiled);
    private static readonly Regex ScheduleRoute = new(@"^/api/projects/([^/]+)/schedule\z", RegexOptions.Compiled);
    private static readonly Regex PromoteRoute = new(@"^/api/projects/([^/]+)/promote\z", RegexOptions.Compiled);
    private static readonly Regex Behaviours = new(@"^/api/projects/([^/]+)/behaviours(?:/([^/]+)/(steps|review|remove|title|paused|priority)(?:/([^/]+))?)?\z", RegexOptions.Compiled);

    /// <inheritdoc />
    public IKitResponse Route(string method, string pathname, string? cookie = null, string? origin = null, JsonElement? body = null)
    {
        if (method == "POST")
        {
            try
            {
                return Write(pathname, cookie, origin, body);
            }
            catch (NotWritableException e)
            {
                // A project read from a repository this Kit has no clone of: nothing was written, and
                // the sentence says why. Caught here rather than per route, so every write answers alike.
                return Json(409, new ApiError { Error = e.ErrorCode, Reason = e.Message });
            }
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
            return Json(200, SessionOf(cookie));
        }

        // BEH-USERS-4: one process for every user, so it needs no session: the repo it comes from is public.
        if (pathname == "/api/process")
        {
            return Process();
        }

        // BEH-USERS-4: the signed-in user's own memory; the id comes from the session, never the request.
        if (pathname == "/api/me/memory")
        {
            return MemoryRead(cookie);
        }

        if (pathname == "/api/projects")
        {
            // BEH-USERS-8: a user's list holds their own projects, and every project this Kit
            // serves is the operator's, so theirs is empty (BEH-USERS-2 adds what their GitHub holds).
            // BEH-SETTINGS-3: the operator keeps the board's projects and gains his personal GitHub's.
            return IsOtherUser(cookie)
                ? UsersProjects(UserOf(cookie)!, [])
                : UsersProjects(UserOf(cookie), corpora.Corpora().Select(viewer.Summary).ToList());
        }

        // BEH-SETTINGS-5: how busy the agent is when nothing is asked of it; a read like the rest (BEH-GATE-H1).
        if (pathname == "/api/settings/idle")
        {
            return Json(200, new IdleSetting
            {
                Idle = corpora.Corpora().Contains(IdleDirective.App, StringComparer.Ordinal) ? IdleDirective.Read(corpora.ReadText(IdleDirective.App)) : IdleDirective.Default,
            });
        }

        // BEH-ACTIVITY-1: recent commits across every project; a read like the rest (BEH-GATE-H1).
        if (pathname == "/api/activity")
        {
            return Json(200, IsOtherUser(cookie) ? new ActivityFeed { Entries = [], NoHistory = [] } : activity.Recent());
        }

        // BEH-NOTIFY-1: the agent's messages, the one read that is NOT served to anyone (BEH-GATE-H1
        // covers projects, which live in a public repo; the feed comes from a private one). Gated as
        // a write is: his session when locked, loopback when not. A user's feed is empty (BEH-USERS-8).
        if (pathname is "/api/notifications" or "/api/push")
        {
            if (lockRequired ? UserOf(cookie) is null : !OriginPolicy.IsLoopback(host))
            {
                return Json(401, new ApiError
                {
                    Error = "not-signed-in",
                    Reason = "the agent's notifications are served only to a signed-in session (or to loopback on a Kit with no password), because they come from a private repository",
                });
            }

            // BEH-NOTIFY-3: whether this Kit can push to his phone, behind the same gate as the feed it announces.
            if (pathname == "/api/push")
            {
                return PushState(cookie);
            }

            return Json(200, IsOtherUser(cookie) ? new NotificationFeed { Entries = [] } : notifications.Recent());
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

        // kit#118: the open notes, apart from the project view so its shape (and its goldens) stay put.
        var notes = NotesRoute.Match(pathname);
        if (notes.Success)
        {
            if (DecodeUriComponent(notes.Groups[1].Value) is not { } app)
            {
                return Json(400, new ApiError { Error = "bad-request", Reason = "the app name is not valid percent-encoding" });
            }

            var known = corpora.Corpora();
            return known.Contains(app, StringComparer.Ordinal)
                ? Json(200, new NoteList { App = app, File = corpora.RelativeNotesPath(app), Notes = Notes.Parse(corpora.ReadNotesText(app)) })
                : Json(404, new ApiError { Error = "no-such-project", Reason = $"no corpus named '{app}'", Known = known.ToList() });
        }

        // BEH-COMMIT-5: this project's edits that have not reached the base, and the pull request
        // they wait in. A read like the rest, so it is served to anyone (BEH-GATE-H1).
        var pm = PendingRoute.Match(pathname);
        if (pm.Success)
        {
            if (DecodeUriComponent(pm.Groups[1].Value) is not { } app)
            {
                return Json(400, new ApiError { Error = "bad-request", Reason = "the app name is not valid percent-encoding" });
            }

            var known = corpora.Corpora();
            return known.Contains(app, StringComparer.Ordinal)
                ? Json(200, Pending(app))
                : Json(404, new ApiError { Error = "no-such-project", Reason = $"no corpus named '{app}'", Known = known.ToList() });
        }

        // BEH-SCHED-1..3: a route of its own for the same reason as the notes.
        var schedule = ScheduleRoute.Match(pathname);
        if (schedule.Success)
        {
            if (DecodeUriComponent(schedule.Groups[1].Value) is not { } app)
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
                return Json(200, ScheduleOf(app));
            }
            catch (ProjectionFailedException e)
            {
                return Json(500, new ApiError { Error = "projection-failed", Reason = e.Message });
            }
        }

        // BEH-BOARD-4: when each built behaviour was completed, apart from the project view as the
        // notes are — a history that cannot be read costs the board its order, never its cards.
        var history = HistoryRoute.Match(pathname);
        if (history.Success)
        {
            if (DecodeUriComponent(history.Groups[1].Value) is not { } app)
            {
                return Json(400, new ApiError { Error = "bad-request", Reason = "the app name is not valid percent-encoding" });
            }

            var known = corpora.Corpora();
            return known.Contains(app, StringComparer.Ordinal)
                ? Json(200, completions.For(app))
                : Json(404, new ApiError { Error = "no-such-project", Reason = $"no corpus named '{app}'", Known = known.ToList() });
        }

        return Json(404, new ApiError { Error = "no-such-route", Reason = $"nothing is served at {pathname}" });
    }

    /// <summary>
    /// The edits branch's commits for <paramref name="app"/> only, newest first, with the pull
    /// request they wait in. Off — write-back off, no edits branch, no reader — is an empty list
    /// with the reason, not an error: a page that lists nothing pending must still render.
    /// </summary>
    private PendingView Pending(string app)
    {
        if (!git.Enabled || string.IsNullOrEmpty(head) || pending is null)
        {
            return new PendingView { App = app, Head = head, Base = baseBranch, Reason = "git write-back is off, so no edit is waiting on a branch" };
        }

        // Sync over async, as Commit does: the host has no synchronisation context to deadlock on.
        var all = pending.PendingAsync(head, baseBranch).GetAwaiter().GetResult();
        var mine = all.Edits.Where(e => string.Equals(e.App, app, StringComparison.Ordinal)).ToList();
        return new PendingView
        {
            App = app,
            Head = head,
            Base = baseBranch,
            Edits = mine,
            // The pull request carries every project's edits; it is only worth showing beside this one's.
            PullNumber = mine.Count > 0 ? all.PullNumber : null,
            PullUrl = mine.Count > 0 ? all.PullUrl : null,
            Reason = all.Reason,
        };
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

        // BEH-USERS-10: the other key, under the same Origin check and above the same lock.
        if (pathname == "/api/session/github")
        {
            return SignInWithGitHub(body);
        }

        // Exclusive on purpose: with a password the session decides and the bind address
        // is irrelevant — there is no "loopback, so allow" while a password is set.
        if (lockRequired)
        {
            if (UserOf(cookie) is not { } who)
            {
                return Json(401, new ApiError
                {
                    Error = "not-signed-in",
                    Reason = "this Kit is password-protected and this request carries no valid session. POST the password to /api/session first.",
                });
            }

            // A user's own account is theirs to change; every other write is to a project.
            if (pathname is "/api/users" or "/api/session/password")
            {
                return Account(pathname, who, body);
            }

            if (pathname == "/api/github/connected")
            {
                return ConnectGitHub(who, cookie, body);
            }

            // BEH-USERS-4: a user's memory is theirs to write, whoever they are.
            if (pathname == "/api/me/memory")
            {
                return MemoryWrite(who, body);
            }

            // BEH-USERS-8: every project this Kit serves is the operator's, so only he writes.
            // When a user's own projects arrive with their GitHub, this asks whose project it is.
            if (who != ISessionStore.Operator)
            {
                return Json(403, new ApiError { Error = "not-yours", Reason = "this project belongs to another user, so this session cannot change it" });
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

        // No password: only loopback got this far, and the one person there is the operator.
        if (pathname == "/api/me/memory")
        {
            return MemoryWrite(ISessionStore.Operator, body);
        }

        // Below the lock like every edit: proposing the edits to `dev` is a write in its own right.
        if (pathname == "/api/commit")
        {
            return Commit();
        }

        if (pathname.StartsWith("/api/push/", StringComparison.Ordinal))
        {
            return PushWrite(pathname, body);
        }

        if (pathname == "/api/settings/idle")
        {
            lock (writeGate)
            {
                return SetIdle(body);
            }
        }

        var bm = Bindings.Match(pathname);
        var nm = NotesRoute.Match(pathname);
        var na = NoteAction.Match(pathname);
        var sm = ScheduleRoute.Match(pathname);
        var pm = PromoteRoute.Match(pathname);
        var m = bm.Success ? bm : nm.Success ? nm : na.Success ? na : sm.Success ? sm : pm.Success ? pm : Behaviours.Match(pathname);
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

        // ONE edit at a time: read the file, write it, commit, push. Kestrel serves requests in
        // parallel, where ui.js never could (one thread, synchronous git), so two edits at once
        // would race on the same corpus text and on git's index lock (kit#165).
        lock (writeGate)
        {
            return bm.Success ? Bind(app, b)
                : nm.Success ? LeaveNote(app, b)
                : na.Success ? ActOnNote(app, na.Groups[2].Value, b)
                : sm.Success ? Reschedule(app, b)
                : pm.Success ? SetPromote(app, b)
                : Edit(app, m, b);
        }
    }

    private readonly object writeGate = new();

    /// <inheritdoc />
    public IGitWriteBack CatchUp(string dir)
    {
        // Behind the edits' own lock (BEH-COMMIT-6): a merge that touched the working tree between
        // an edit's write and its commit would be committed as part of the edit, or break it.
        lock (writeGate)
        {
            return git.CatchUp(dir);
        }
    }

    /// <summary><c>write()</c> past its gates: create, add or replace a step, retitle, adjudicate, or remove.</summary>
    private KitResponse Edit(string app, Match m, JsonElement body)
    {
        var kind = m.Groups[3].Value;
        var review = kind == "review";
        var remove = kind == "remove";
        var title = kind == "title";
        var paused = kind == "paused";
        var priority = kind == "priority";

        // `steps/{n}` replaces step n (BEH-ACT-2); bare `steps` appends one. Nothing else takes an index.
        var update = m.Groups[4].Success;
        if (update && kind != "steps")
        {
            return Json(404, new ApiError { Error = "no-such-route", Reason = $"nothing accepts a POST at {m.Value}" });
        }

        var index = -1;
        if (update && !int.TryParse(m.Groups[4].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out index))
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "a step index is a whole number, counted from 0" });
        }

        var step = kind == "steps" && !update;

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

        // Removal takes no fields (the id is in the path): any property at all is refused, so a
        // client that thinks it can say more than "remove this" finds out rather than being ignored.
        if (remove && body.ValueKind == JsonValueKind.Object && body.EnumerateObject().Any())
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "removing a behaviour takes an empty body: the id is in the path" });
        }

        // BEH-BOARD-16: the one boolean body. It is named, not toggled, so a double press is a no-op.
        if (paused && Field(body, "paused") is not { ValueKind: JsonValueKind.True or JsonValueKind.False })
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "paused must be true or false" });
        }

        // The score is a whole number 1 (first) to 5; anything else is a bad request, not a corpus refusal.
        if (priority && !(Field(body, "priority") is { ValueKind: JsonValueKind.Number } n && n.TryGetInt32(out var score) && score is >= 1 and <= 5))
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "priority must be a whole number from 1 to 5" });
        }

        if (!remove && !paused && !priority && TypeError(body, review ? ReviewFields : title ? TitleFields : step || update ? StepFields : CreateFields) is { } wrongType)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = wrongType });
        }

        var text = corpora.ReadText(app);
        var result = remove
            ? writer.RemoveBehaviour(text, id!)
            : update
            ? writer.UpdateStep(text, id!, index, Str(body, "step")!)
            : paused
            ? writer.SetPaused(text, id!, Field(body, "paused")!.Value.GetBoolean())
            : priority
            ? writer.SetPriority(text, id!, Field(body, "priority")!.Value.GetInt32())
            : title
            ? writer.Retitle(text, id!, Str(body, "title")!)
            : review
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
        var what = remove ? $"remove {id}" : update ? $"update step {index} of {id}" : title ? $"retitle {id}" : priority ? $"set the priority of {id} to {Field(body, "priority")!.Value.GetInt32()}" : paused ? $"{(Field(body, "paused")!.Value.GetBoolean() ? "pause" : "resume")} {id}" : review ? $"adjudicate {id}" : step ? $"add a step to {id}" : $"add {id}";
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
    /// kit#118: append one free-text note to <c>&lt;app&gt;.notes.md</c>, then commit and push it like
    /// any edit. A note on a behaviour names it, but the id is not looked up: a note may be about a
    /// behaviour that is being renamed, removed, or does not exist yet.
    /// <para>
    /// <c>kind</c> (kit#118, 2026-10-12): <c>request</c> asks for a behaviour in prose instead of the
    /// id/title/actor form (BEH-NOTE-4); <c>resolution</c> says how a decision on the question sheet
    /// should go, naming it by <c>resolves</c> (BEH-NOTE-8). Neither touches the corpus.
    /// </para>
    /// </summary>
    private KitResponse LeaveNote(string app, JsonElement body)
    {
        if (TypeError(body, NoteFields) is { } wrongType)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = wrongType });
        }

        var kind = Str(body, "kind") ?? Notes.NoteKind;
        var behaviour = Str(body, "behaviour");
        var resolves = Str(body, "resolves");
        if (kind is not (Notes.NoteKind or Notes.RequestKind or Notes.ResolutionKind or Notes.BellKind))
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "kind must be note, request, resolution or bell" });
        }

        // A bell needs no words (BEH-BELL-1): ringing it is the whole message. Every other kind does.
        var text = Str(body, "text") ?? "";
        var bell = kind == Notes.BellKind;
        if ((!bell || text.Length > 0) && TextError(text) is { } badText)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = badText });
        }

        if (behaviour is not null && (kind is not (Notes.NoteKind or Notes.BellKind) || !Notes.IsBehaviourId(behaviour)))
        {
            return Json(400, new ApiError
            {
                Error = "bad-request",
                Reason = kind is not (Notes.NoteKind or Notes.BellKind)
                    ? "only a plain note or a bell can name a behaviour"
                    : "behaviour must be an id such as BEH-1: letters, digits, '.', '_' and '-' only",
            });
        }

        if (bell && behaviour is null)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "a bell names the item it rings: behaviour must be its id" });
        }

        if ((kind == Notes.ResolutionKind) != (resolves is not null) || (resolves is not null && !Notes.IsQuestionKey(resolves)))
        {
            return Json(400, new ApiError
            {
                Error = "bad-request",
                Reason = kind == Notes.ResolutionKind
                    ? $"a resolution names the decision it resolves: resolves must be its key, one token of at most {Notes.MaxKeyLength} characters"
                    : "only a resolution names a decision to resolve",
            });
        }

        var existing = corpora.ReadNotesText(app);
        if (bell && Notes.IsRung(existing, behaviour!))
        {
            // Ringing again before the agent answers adds nothing (BEH-BELL-1), so nothing is written.
            return Json(409, new ApiError { Error = "already-rung", Reason = $"{behaviour}'s bell is already rung; the agent has not answered it yet" });
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var (written, summary) = kind switch
        {
            Notes.BellKind => (Notes.AppendBell(existing, app, behaviour!, text, now), $"ring {behaviour}'s bell"),
            Notes.RequestKind => (Notes.AppendRequest(existing, app, text, now), "ask for a behaviour"),
            Notes.ResolutionKind => (Notes.AppendResolution(existing, app, text, resolves!, now), $"resolve {resolves}"),
            _ => (Notes.Append(existing, app, text, behaviour, now), behaviour is null ? "leave a note" : $"leave a note on {behaviour}"),
        };

        corpora.WriteNotesText(app, written);
        return Json(200, GitOutcome(corpora.FullNotesPath(app), summary, app, new WriteOutcome
        {
            App = app,
            Behaviour = behaviour,
            File = corpora.RelativeNotesPath(app),
            Note = NotCommitted,
        }));
    }

    /// <summary>
    /// Answer a note's questions (BEH-NOTE-6) or dismiss it (BEH-NOTE-7). The note is named by its
    /// place in the file AND its date, so an edit that lands between reading the list and pressing
    /// the button cannot redirect the action onto a different note: it is refused with 409 instead.
    /// </summary>
    private KitResponse ActOnNote(string app, string action, JsonElement body)
    {
        var dismiss = action == "dismiss";
        if (TypeError(body, dismiss ? DismissFields : ReplyFields) is { } wrongType)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = wrongType });
        }

        var index = body.GetProperty("index").GetInt32();
        var at = Str(body, "at")!;
        var text = dismiss ? null : Str(body, "text")!;
        if (text is not null && TextError(text) is { } badText)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = badText });
        }

        var existing = corpora.ReadNotesText(app);
        var written = dismiss
            ? Notes.Dismiss(existing, index, at)
            : Notes.Reply(existing, index, at, text!, (clock ?? TimeProvider.System).GetUtcNow());
        if (written is null)
        {
            return Json(409, new ApiError
            {
                Error = "no-such-note",
                Reason = $"note {index} left at {at} is no longer in {corpora.RelativeNotesPath(app)} — it may have been folded or dismissed already. Reload to see what is open.",
            });
        }

        corpora.WriteNotesText(app, written);
        return Json(200, GitOutcome(corpora.FullNotesPath(app), dismiss ? $"dismiss the note left at {at}" : $"answer the note left at {at}", app, new WriteOutcome
        {
            App = app,
            File = corpora.RelativeNotesPath(app),
            Note = NotCommitted,
        }));
    }

    /// <summary>
    /// The schedule as BEH-SCHED-1 and -3 describe it: every listed id in order, each pending, done or
    /// missing, the first pending one marked next; then every pending behaviour not listed, in corpus
    /// order. Nothing here falls back to the backlog — with nothing scheduled, nothing is next.
    /// </summary>
    private ScheduleList ScheduleOf(string app)
    {
        var behaviours = viewer.View(app).Behaviours;
        var byId = new Dictionary<string, (string Title, bool Pending)>(StringComparer.Ordinal);
        foreach (var b in behaviours)
        {
            byId.TryAdd(b.Id, (b.Title, b.Pending));
        }

        var ids = Schedule.Parse(corpora.ReadScheduleText(app));
        var scheduled = new List<ScheduleEntry>();
        var nextTaken = false;
        foreach (var id in ids)
        {
            var known = byId.TryGetValue(id, out var b);
            var state = !known ? "missing" : b.Pending ? "pending" : "done";
            var next = state == "pending" && !nextTaken;
            nextTaken |= next;
            scheduled.Add(new ScheduleEntry { Id = id, Title = known ? b.Title : null, State = state, Next = next });
        }

        var backlog = behaviours
            .Where(b => b.Pending && !ids.Contains(b.Id, StringComparer.Ordinal))
            .DistinctBy(b => b.Id, StringComparer.Ordinal)
            .Select(b => new ScheduleEntry { Id = b.Id, Title = b.Title, State = "pending" })
            .ToList();

        return new ScheduleList { App = app, File = corpora.RelativeSchedulePath(app), Scheduled = scheduled, Backlog = backlog };
    }

    /// <summary>
    /// BEH-SETTINGS-4: turn the agent's grant to promote this project's <c>dev</c> to <c>main</c> on or
    /// off — the <c># kit:promote self</c> line in the project's primary file, committed like any edit.
    /// Operator-only, by the gate every write sits behind. Named, not toggled, so a double press is a no-op.
    /// Only the primary file (named for its project) takes it; a secondary file is refused, saying where.
    /// </summary>
    private KitResponse SetPromote(string app, JsonElement body)
    {
        if (Field(body, "promote") is not { ValueKind: JsonValueKind.True or JsonValueKind.False } flag)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "promote must be true or false" });
        }

        string project;
        try
        {
            project = viewer.View(app).Project;
        }
        catch (ProjectionFailedException e)
        {
            return Json(500, new ApiError { Error = "projection-failed", Reason = e.Message });
        }

        if (project != app)
        {
            return Json(409, new ApiError { Error = "not-primary", Reason = $"{app} is part of project {project}; the grant lives in {project}'s own file, so set it there" });
        }

        var on = flag.GetBoolean();
        var result = writer.SetPromote(corpora.ReadText(app), on);
        if (!result.Ok)
        {
            return Json(409, new ApiError { Error = result.Error!, Reason = result.Reason! });
        }

        corpora.WriteText(app, result.Text!);
        return Json(200, GitOutcome(corpora.FullPath(app), on ? $"let the agent promote {app} to main" : $"stop the agent promoting {app} to main", app, new WriteOutcome { App = app, File = corpora.RelativePath(app), Note = NotCommitted }));
    }

    /// <summary>
    /// BEH-SETTINGS-5: choose how busy the agent is when nothing is asked of it — the
    /// <c># kit:idle</c> line in Kit's own primary file, committed like any edit. Operator-only, by the
    /// gate every write sits behind. Named, not toggled, so a double press is a no-op.
    /// </summary>
    private KitResponse SetIdle(JsonElement? body)
    {
        if (body is not { ValueKind: JsonValueKind.Object } b
            || Field(b, "idle") is not { ValueKind: JsonValueKind.String } mode
            || !IdleDirective.Modes.Contains(mode.GetString()!))
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = $"idle must be one of {string.Join(", ", IdleDirective.Modes)}" });
        }

        var app = IdleDirective.App;
        if (!corpora.Corpora().Contains(app, StringComparer.Ordinal))
        {
            return Json(404, new ApiError { Error = "no-such-project", Reason = $"the setting lives in the corpus named '{app}', which this Kit does not serve" });
        }

        var chosen = mode.GetString()!;
        var result = writer.SetIdle(corpora.ReadText(app), chosen);
        if (!result.Ok)
        {
            return Json(409, new ApiError { Error = result.Error!, Reason = result.Reason! });
        }

        corpora.WriteText(app, result.Text!);
        return Json(200, GitOutcome(corpora.FullPath(app), $"set the agent's idle behaviour to {chosen}", app, new WriteOutcome { App = app, File = corpora.RelativePath(app), Note = NotCommitted }));
    }

    /// <summary>
    /// BEH-SCHED-2: schedule a backlog behaviour (appended), return one to the backlog, or move one up
    /// or down a place — then commit and push the schedule like any edit. The corpus is never touched.
    /// Only a pending behaviour can be scheduled; anything already listed can be removed or moved,
    /// a built or vanished one included, so a stale entry can always be cleared.
    /// </summary>
    private KitResponse Reschedule(string app, JsonElement body)
    {
        if (TypeError(body, ScheduleFields) is { } wrongType)
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = wrongType });
        }

        var action = Str(body, "action")!;
        var id = Str(body, "id")!;
        if (action is not (Schedule.Add or Schedule.Remove or Schedule.Up or Schedule.Down))
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "action must be add, remove, up or down" });
        }

        // One word on one line, or it would not read back as the id that was written.
        if (!Notes.IsBehaviourId(id))
        {
            return Json(400, new ApiError { Error = "bad-request", Reason = "id must be a behaviour id such as BEH-1: letters, digits, '.', '_' and '-' only" });
        }

        if (action == Schedule.Add)
        {
            IReadOnlyList<Abstractions.DataModels.IBehaviourView> behaviours;
            try
            {
                behaviours = viewer.View(app).Behaviours;
            }
            catch (ProjectionFailedException e)
            {
                return Json(500, new ApiError { Error = "projection-failed", Reason = e.Message });
            }

            if (behaviours.FirstOrDefault(b => b.Id == id) is not { } found)
            {
                return Json(409, new ApiError { Error = "no-such-behaviour", Reason = $"{app} has no behaviour {id}" });
            }

            if (!found.Pending)
            {
                return Json(409, new ApiError { Error = "not-pending", Reason = $"{id} is already built; only a pending behaviour can be scheduled" });
            }
        }

        var (ids, error, reason) = Schedule.Change(Schedule.Parse(corpora.ReadScheduleText(app)), action, id);
        if (ids is null)
        {
            return Json(409, new ApiError { Error = error!, Reason = reason! });
        }

        corpora.WriteScheduleText(app, Schedule.Render(app, ids));
        return Json(200, GitOutcome(corpora.FullSchedulePath(app), Schedule.Summary(action, id), app, new WriteOutcome
        {
            App = app,
            Behaviour = id,
            File = corpora.RelativeSchedulePath(app),
            Note = NotCommitted,
        }));
    }

    private static string? TextError(string text) =>
        string.IsNullOrWhiteSpace(text) ? "a note needs some text"
        : text.Length > Notes.MaxLength ? $"a note is at most {Notes.MaxLength} characters; this one is {text.Length}"
        : null;

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
                    Warning = offReason is null ? DeployedWarning : $"{DeployedWarning} Why: {offReason}.",
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
    private static readonly (string Field, string Type)[] TitleFields = [("title", "string")];
    private static readonly (string Field, string Type)[] BindFields = [("noun", "string")];
    private static readonly (string Field, string Type)[] NoteFields = [("text", "string?"),("behaviour", "string?"), ("kind", "string?"), ("resolves", "string?")];
    private static readonly (string Field, string Type)[] ReplyFields = [("index", "int"), ("at", "string"), ("text", "string")];
    private static readonly (string Field, string Type)[] DismissFields = [("index", "int"), ("at", "string")];
    private static readonly (string Field, string Type)[] ScheduleFields = [("action", "string"), ("id", "string")];

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

            if (type == "int" && !(v is { ValueKind: JsonValueKind.Number } n && n.TryGetInt32(out _)))
            {
                return $"{field} must be a whole number";
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

        // `typeof given === 'string' ? given : ''` — a missing, numeric or array password is ''.
        var given = body is { ValueKind: JsonValueKind.Object } b && b.TryGetProperty("password", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()!
            : string.Empty;

        // BEH-USERS-1: no name, or his, is the operator signing in exactly as before accounts.
        var name = body is { } nb ? Str(nb, "username") : null;
        var user = string.IsNullOrEmpty(name) || name == operatorName ? ISessionStore.Operator : name;

        // Check, judge and count as ONE step. Separately, guesses sent at once were all checked
        // before any was counted: 200 parallel guesses had 30 judged against 5 free attempts. ui.js
        // never could, being one thread; the port made sign-in concurrent (SignInRaceTests).
        lock (signInGate)
        {
            var wait = throttle?.RetryAfterMs() ?? 0;
            if (wait > 0)
            {
                var seconds = (int)Math.Ceiling(wait / 1000.0);
                return Json(429, new ApiError { Error = "too-many-attempts", Reason = $"too many failed sign-ins; try again in {seconds}s", RetryAfterSeconds = seconds });
            }

            if (!(user == ISessionStore.Operator ? SecretsMatch(given, password!) : IsUsersPassword(user, given)))
            {
                throttle?.Fail();

                // One message for missing and wrong, and for an unknown user: telling them apart helps a guesser.
                return Json(401, new ApiError { Error = "bad-password", Reason = "that is not the password" });
            }

            throttle?.Succeed();
        }

        // BEH-USERS-10: once their GitHub is connected, a user's password signs them in no more.
        // Said only to someone who knew it, so it tells a guesser nothing about who has connected.
        if (user != ISessionStore.Operator && SignsInWithGitHub(user))
        {
            return Json(401, new ApiError { Error = "github-sign-in", Reason = "this account signs in with GitHub now: use Sign in with GitHub" });
        }

        var token = sessions!.Create(user);

        // The token goes out ONLY as an HttpOnly cookie, never in the body.
        return new KitResponse { Status = 200, ContentType = "application/json", Body = new SignInState { Ok = true, SignedIn = true }, SetCookie = CookieFor(token) };
    }

    private readonly object signInGate = new();

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
