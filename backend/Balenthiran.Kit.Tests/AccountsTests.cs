using System.Collections.Concurrent;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-USERS-1</c> (accounts James creates, whose users change their password) and
/// <c>BEH-USERS-8</c> (a user never reaches the operator's work, whatever the URL).
/// </summary>
public class AccountsTests
{
    private const string Operator = "operator-pw";
    private const string AnnPassword = "ann-first-password";

    [Fact]
    public void With_accounts_off_the_session_answer_has_no_new_fields()
    {
        var router = Router(users: null);
        var state = Assert.IsType<SessionState>(router.Route("GET", "/api/session").Body);

        // The auth goldens hold this shape byte for byte; the new fields must not leak into it.
        Assert.Null(state.Accounts);
        Assert.Null(state.User);
        Assert.Equal(404, router.Route("POST", "/api/users", Cookie(SignIn(router, null, Operator)), body: Json(new { username = "ann", password = AnnPassword })).Status);
    }

    [Fact]
    public void The_operator_signs_in_with_or_without_his_name()
    {
        var router = Router(new MemoryUsers());

        foreach (var name in new string?[] { null, KitRouter.DefaultOperator })
        {
            var state = Assert.IsType<SessionState>(router.Route("GET", "/api/session", Cookie(SignIn(router, name, Operator))).Body);
            Assert.True(state.Accounts);
            Assert.Equal(KitRouter.DefaultOperator, state.User);
        }
    }

    [Fact]
    public void He_creates_an_account_and_its_user_signs_in_with_it()
    {
        var users = new MemoryUsers();
        var router = Router(users);
        var him = Cookie(SignIn(router, null, Operator));

        var created = router.Route("POST", "/api/users", him, body: Json(new { username = "ann", password = AnnPassword }));
        Assert.Equal(201, created.Status);
        Assert.Equal("ann", Assert.IsType<AccountState>(created.Body).User);

        // The store holds a hash, never the password.
        Assert.DoesNotContain(AnnPassword, users.HashOf("ann")!, StringComparison.Ordinal);

        var ann = Cookie(SignIn(router, "ann", AnnPassword));
        var hers = Assert.IsType<SessionState>(router.Route("GET", "/api/session", ann).Body);
        Assert.Equal("ann", hers.User);
        Assert.Null(hers.Operator);
        Assert.True(Assert.IsType<SessionState>(router.Route("GET", "/api/session", him).Body).Operator);
    }

    [Fact]
    public void A_wrong_password_and_an_unknown_user_answer_alike()
    {
        var router = Router(Seeded());

        var wrong = router.Route("POST", "/api/session", body: Json(new { username = "ann", password = "not-her-password" }));
        var ghost = router.Route("POST", "/api/session", body: Json(new { username = "ghost", password = AnnPassword }));
        var bad = router.Route("POST", "/api/session", body: Json(new { username = "Not.A.Name", password = AnnPassword }));

        foreach (var r in new[] { wrong, ghost, bad })
        {
            Assert.Equal(401, r.Status);
            Assert.Equal("bad-password", Assert.IsType<ApiError>(r.Body).Error);
            Assert.Null(r.SetCookie);
        }
    }

    [Fact]
    public void Another_users_operator_password_does_not_sign_them_in()
    {
        // KIT_PASSWORD is his alone: under a user's name it is just a wrong password.
        var router = Router(Seeded());
        Assert.Equal(401, router.Route("POST", "/api/session", body: Json(new { username = "ann", password = Operator })).Status);
    }

    [Fact]
    public void A_user_cannot_write_to_the_operators_projects()
    {
        var router = Router(Seeded());
        var ann = Cookie(SignIn(router, "ann", AnnPassword));
        var him = Cookie(SignIn(router, null, Operator));

        // One of every write route, so a route added above the gate shows here (BEH-USERS-8: "whatever the URL").
        var paths = new[]
        {
            "/api/commit", "/api/projects/kit/notes", "/api/projects/kit/notes/reply", "/api/projects/kit/notes/dismiss",
            "/api/projects/kit/schedule", "/api/projects/kit/bindings", "/api/projects/kit/behaviours",
            "/api/projects/kit/behaviours/BEH-1/title", "/api/projects/kit/behaviours/BEH-1/review",
            "/api/projects/kit/behaviours/BEH-1/paused", "/api/projects/kit/behaviours/BEH-1/remove",
            "/api/projects/kit/behaviours/BEH-1/priority", "/api/projects/kit/promote",
            "/api/projects/kit/behaviours/BEH-1/steps/0",
        };

        foreach (var path in paths)
        {
            var refused = router.Route("POST", path, ann, body: Json(new { }));
            Assert.Equal(403, refused.Status);
            Assert.Equal("not-yours", Assert.IsType<ApiError>(refused.Body).Error);

            // The same request from him passes the gate, whatever the route then makes of it.
            var his = router.Route("POST", path, him, body: Json(new { }));
            Assert.NotEqual(401, his.Status);
            Assert.NotEqual(403, his.Status);
        }
    }

    [Fact]
    public void A_users_project_list_and_activity_hold_nothing_of_his()
    {
        var router = Router(Seeded());
        var ann = Cookie(SignIn(router, "ann", AnnPassword));
        var him = Cookie(SignIn(router, null, Operator));

        Assert.Empty(Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", ann).Body).Projects);
        Assert.Empty(Assert.IsType<ActivityFeed>(router.Route("GET", "/api/activity", ann).Body).Entries);

        // Not an empty Kit: he, and an anonymous reader (BEH-GATE-H1), still see the projects.
        Assert.NotEmpty(Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", him).Body).Projects);
        Assert.NotEmpty(Assert.IsType<ProjectList>(router.Route("GET", "/api/projects").Body).Projects);
        Assert.NotEmpty(Assert.IsType<ActivityFeed>(router.Route("GET", "/api/activity", him).Body).Entries);
    }

    [Fact]
    public void A_users_notifications_hold_none_of_the_agents_messages()
    {
        // BEH-NOTIFY-1: the agent works for him, so its messages are his like the rest (BEH-USERS-8).
        var router = Router(Seeded());
        var ann = Cookie(SignIn(router, "ann", AnnPassword));
        var him = Cookie(SignIn(router, null, Operator));

        var hers = Assert.IsType<NotificationFeed>(router.Route("GET", "/api/notifications", ann).Body);
        Assert.Empty(hers.Entries);
        Assert.Null(hers.Reason);
        Assert.Null(hers.Schedule); // BEH-NOTIFY-5: nor when it next wakes
        Assert.NotEmpty(Assert.IsType<NotificationFeed>(router.Route("GET", "/api/notifications", him).Body).Entries);

        // Unlike the projects (BEH-GATE-H1), the feed comes from a private repo: no session, no feed.
        var anonymous = router.Route("GET", "/api/notifications");
        Assert.Equal(401, anonymous.Status);
        Assert.Equal("not-signed-in", Assert.IsType<ApiError>(anonymous.Body).Error);
    }

    [Fact]
    public void A_user_may_still_read_one_of_his_projects_by_its_url()
    {
        // His projects live in a public repo, so a URL serves them to a user as to anyone (BEH-GATE-H1).
        var router = Router(Seeded());
        var ann = Cookie(SignIn(router, "ann", AnnPassword));

        foreach (var path in new[] { "/api/projects/kit", "/api/projects/kit/notes", "/api/projects/kit/schedule", "/api/projects/kit/pending" })
        {
            var hers = router.Route("GET", path, ann);
            var anyone = router.Route("GET", path);
            Assert.Equal(anyone.Status, hers.Status);
            Assert.Equal(JsonSerializer.Serialize(anyone.Body), JsonSerializer.Serialize(hers.Body));
        }

        Assert.Equal(200, router.Route("GET", "/api/projects/kit", ann).Status);
    }

    [Fact]
    public void Only_the_operator_creates_accounts()
    {
        var router = Router(Seeded());
        var ann = Cookie(SignIn(router, "ann", AnnPassword));

        var r = router.Route("POST", "/api/users", ann, body: Json(new { username = "bob", password = AnnPassword }));
        Assert.Equal(403, r.Status);
        Assert.Equal(401, router.Route("POST", "/api/users", body: Json(new { username = "bob", password = AnnPassword })).Status);
    }

    [Theory]
    [InlineData("ann", "a-good-long-password", 409, "user-exists")]
    [InlineData(KitRouter.DefaultOperator, "a-good-long-password", 409, "user-exists")]
    [InlineData("Ann", "a-good-long-password", 400, "bad-username")]
    [InlineData("a.b", "a-good-long-password", 400, "bad-username")]
    [InlineData("x", "a-good-long-password", 400, "bad-username")]
    [InlineData("bob", "short", 400, "bad-new-password")]
    public void An_account_he_cannot_create_says_why(string name, string password, int status, string error)
    {
        var router = Router(Seeded());
        var r = router.Route("POST", "/api/users", Cookie(SignIn(router, null, Operator)), body: Json(new { username = name, password }));
        Assert.Equal(status, r.Status);
        Assert.Equal(error, Assert.IsType<ApiError>(r.Body).Error);
    }

    [Fact]
    public void A_user_changes_the_password_he_gave_them()
    {
        var router = Router(Seeded());
        var ann = Cookie(SignIn(router, "ann", AnnPassword));
        const string Next = "ann-chose-this-one";

        Assert.Equal(401, router.Route("POST", "/api/session/password", ann, body: Json(new { current = "wrong-wrong-wrong", next = Next })).Status);
        Assert.Equal(400, router.Route("POST", "/api/session/password", ann, body: Json(new { current = AnnPassword, next = "short" })).Status);
        Assert.Equal(200, router.Route("POST", "/api/session/password", ann, body: Json(new { current = AnnPassword, next = Next })).Status);

        Assert.Equal(401, router.Route("POST", "/api/session", body: Json(new { username = "ann", password = AnnPassword })).Status);
        Assert.Equal(200, router.Route("POST", "/api/session", body: Json(new { username = "ann", password = Next })).Status);
    }

    [Fact]
    public void The_operators_password_is_not_changed_through_kit()
    {
        var router = Router(Seeded());
        var r = router.Route("POST", "/api/session/password", Cookie(SignIn(router, null, Operator)), body: Json(new { current = Operator, next = "a-good-long-password" }));
        Assert.Equal(409, r.Status);
        Assert.Equal("operator-password", Assert.IsType<ApiError>(r.Body).Error);
    }

    [Fact]
    public void A_signed_token_names_its_user_and_a_pre_accounts_token_is_his()
    {
        var key = SignedSessionStore.KeyFor(Operator);
        var store = new SignedSessionStore(key);

        var ann = store.Create("ann");
        Assert.Equal("ann", store.UserOf(ann));
        Assert.Equal(ISessionStore.Operator, store.UserOf(store.Create()));

        // A token minted before accounts has the two-part body the operator's still has.
        var legacy = store.Create(ISessionStore.Operator);
        Assert.Equal(2, legacy.Split('.').Length - 1);

        // Renaming the user inside a signed token breaks its signature.
        var forged = ann.Replace(".ann.", ".bob.", StringComparison.Ordinal);
        Assert.Null(store.UserOf(forged));
    }

    [Fact]
    public void A_hash_verifies_its_password_and_nothing_else()
    {
        var hash = PasswordHash.Hash("correct horse battery", iterations: 1000);

        Assert.True(PasswordHash.Verify("correct horse battery", hash));
        Assert.False(PasswordHash.Verify("correct horse batterY", hash));
        Assert.False(PasswordHash.Verify("correct horse battery", null));
        Assert.False(PasswordHash.Verify("correct horse battery", "pbkdf2-sha256$1000$not-base64$x"));
        Assert.False(PasswordHash.Verify("correct horse battery", hash.Replace("pbkdf2-sha256", "md5", StringComparison.Ordinal)));
        Assert.NotEqual(hash, PasswordHash.Hash("correct horse battery", iterations: 1000));
    }

    private static MemoryUsers Seeded()
    {
        var users = new MemoryUsers();
        users.Add("ann", PasswordHash.Hash(AnnPassword, iterations: 1000));
        return users;
    }

    private static string SignIn(KitRouter router, string? name, string password)
    {
        var r = router.Route("POST", "/api/session", body: Json(name is null ? new { password } : (object)new { username = name, password }));
        Assert.Equal(200, r.Status);
        return r.SetCookie!.Split(';')[0];
    }

    private static string Cookie(string pair) => pair;

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static KitRouter Router(IUserStore? users)
    {
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        return new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            Operator,
            new OriginPolicy(new UrlParser(), null),
            new SessionStore(),
            new SignInThrottle(),
            activity: new OneCommit(),
            users: users,
            notifications: new OneNotification());
    }

    internal sealed class MemoryUsers : IUserStore
    {
        private readonly ConcurrentDictionary<string, string> hashes = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, IGitHubLink> links = new(StringComparer.Ordinal);

        public IGitHubLink? GitHubOf(string name) => links.GetValueOrDefault(name);

        public bool SetGitHub(string name, IGitHubLink link)
        {
            // As the table's UNIQUE: one installation, one user.
            if (!hashes.ContainsKey(name) || links.Any(l => l.Key != name && l.Value.Installation == link.Installation))
            {
                return false;
            }

            links[name] = link;
            return true;
        }

        public string? UserOfInstallation(long installation) => links.FirstOrDefault(l => l.Value.Installation == installation).Key;

        public string? HashOf(string name) => hashes.GetValueOrDefault(name);

        public bool Add(string name, string hash) => hashes.TryAdd(name, hash);

        public bool SetHash(string name, string hash)
        {
            if (!hashes.ContainsKey(name))
            {
                return false;
            }

            hashes[name] = hash;
            return true;
        }
    }

    private sealed class OneCommit : IActivityReader
    {
        public IActivityFeed Recent() => new ActivityFeed { Entries = [new ActivityEntry { App = "kit", Commit = "abc", Summary = "x", At = "2026-10-23T00:00:00Z" }], NoHistory = [] };
    }

    private sealed class OneNotification : INotificationReader
    {
        public INotificationFeed Recent() => new NotificationFeed { Entries = [new Notification { At = "2026-10-24T14:00:00Z", Kind = "wake", Text = "x" }] };

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
