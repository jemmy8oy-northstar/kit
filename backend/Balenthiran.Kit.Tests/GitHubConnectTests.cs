using System.Net;
using System.Text.Json;
using System.Web;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-USERS-1</c>'s second half: once signed in, a user connects their GitHub by
/// installing Kit's GitHub App. The installation recorded is only ever one GitHub lists for the
/// user who authorised; the id the install redirect carries can be typed by anyone.
/// </summary>
public class GitHubConnectTests
{
    private const string Operator = "operator-pw";
    private const string Secret = "the-client-secret";

    [Fact]
    public void A_user_connects_the_installation_GitHub_lists_for_them()
    {
        var users = Seeded();
        var router = Router(users, new Reachable(new GitHubLink { Installation = 42, Account = "ann-gh" }));
        var ann = SignIn(router, "ann");

        var before = Session(router, ann);
        Assert.Null(before.GitHub);
        Assert.Equal("https://github.example/apps/kit/installations/new", before.GitHubInstall);

        var r = Connect(router, ann, new { code = "c0de", state = StateOf(before), installation = "42" });
        Assert.Equal(200, r.Status);
        Assert.Equal("ann-gh", Assert.IsType<SessionState>(r.Body).GitHub);
        Assert.Equal(42, users.GitHubOf("ann")!.Installation);
        Assert.Equal("ann-gh", Session(router, ann).GitHub);
    }

    [Fact]
    public void An_installation_id_GitHub_does_not_list_for_them_is_refused()
    {
        // The spoof GitHub's docs warn of: a real code, someone else's installation id.
        var users = Seeded();
        var router = Router(users, new Reachable(new GitHubLink { Installation = 42, Account = "ann-gh" }));
        var ann = SignIn(router, "ann");

        var r = Connect(router, ann, new { code = "c0de", state = StateOf(Session(router, ann)), installation = 7 });
        Assert.Equal(403, r.Status);
        Assert.Equal("not-your-installation", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Null(users.GitHubOf("ann"));
    }

    [Fact]
    public void Without_a_hint_one_installation_is_taken_and_none_or_several_are_explained()
    {
        foreach (var (reachable, status, error) in new (GitHubLink[], int, string?)[]
        {
            ([new GitHubLink { Installation = 42, Account = "ann-gh" }], 200, null),
            ([], 409, "no-installation"),
            ([new GitHubLink { Installation = 42, Account = "ann-gh" }, new GitHubLink { Installation = 43, Account = "ann-org" }], 409, "several-installations"),
        })
        {
            var router = Router(Seeded(), new Reachable(reachable));
            var ann = SignIn(router, "ann");
            var r = Connect(router, ann, new { code = "c0de", state = StateOf(Session(router, ann)) });
            Assert.Equal(status, r.Status);
            Assert.Equal(error, (r.Body as ApiError)?.Error);
        }
    }

    [Fact]
    public void A_state_made_for_another_user_or_gone_stale_is_refused_before_GitHub_is_asked()
    {
        var users = Seeded();
        users.Add("bob", PasswordHash.Hash("bob-first-password", iterations: 1000));
        var clock = new Clock(DateTimeOffset.Parse("2026-10-23T10:00:00Z"));
        var github = new Reachable(new GitHubLink { Installation = 42, Account = "ann-gh" });
        var router = Router(users, github, clock);
        var ann = SignIn(router, "ann");
        var bob = SignIn(router, "bob", "bob-first-password");

        // Bob's authorisation, finished in Ann's browser, is the login-CSRF the state exists for.
        var bobs = StateOf(Session(router, bob));
        Assert.Equal("bad-state", Error(Connect(router, ann, new { code = "c0de", state = bobs })));

        var hers = StateOf(Session(router, ann));
        clock.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal("bad-state", Error(Connect(router, ann, new { code = "c0de", state = hers })));
        Assert.Equal("bad-state", Error(Connect(router, ann, new { code = "c0de" })));
        Assert.Equal("bad-state", Error(Connect(router, ann, new { code = "c0de", state = "9999999999.00" })));

        Assert.Equal(0, github.Asked);
        Assert.Null(users.GitHubOf("ann"));
    }

    [Fact]
    public void One_installation_belongs_to_one_user()
    {
        var users = Seeded();
        users.Add("bob", PasswordHash.Hash("bob-first-password", iterations: 1000));
        var router = Router(users, new Reachable(new GitHubLink { Installation = 42, Account = "shared-org" }));

        var ann = SignIn(router, "ann");
        Assert.Equal(200, Connect(router, ann, new { code = "a", state = StateOf(Session(router, ann)) }).Status);

        var bob = SignIn(router, "bob", "bob-first-password");
        Assert.Equal("github-taken", Error(Connect(router, bob, new { code = "b", state = StateOf(Session(router, bob)) })));
        Assert.Null(users.GitHubOf("bob"));
    }

    [Fact]
    public void The_operator_is_offered_nothing_and_cannot_connect()
    {
        var router = Router(Seeded(), new Reachable());
        var him = SignIn(router, null, Operator);

        var state = Session(router, him);
        Assert.Null(state.GitHubInstall);
        Assert.Null(state.GitHubAuthorize);
        Assert.Equal("operator-github", Error(Connect(router, him, new { code = "c", state = "x" })));
    }

    [Fact]
    public void Without_a_connector_the_session_is_unchanged_and_the_route_is_absent()
    {
        var router = Router(Seeded(), github: null);
        var ann = SignIn(router, "ann");

        var state = Session(router, ann);
        Assert.Null(state.GitHubInstall);
        Assert.Null(state.GitHubAuthorize);
        Assert.Equal(404, Connect(router, ann, new { code = "c", state = "x" }).Status);
    }

    [Fact]
    public void A_GitHub_failure_is_a_502_with_its_reason()
    {
        var router = Router(Seeded(), new Reachable { Fails = "GitHub refused the authorisation code" });
        var ann = SignIn(router, "ann");
        var r = Connect(router, ann, new { code = "c", state = StateOf(Session(router, ann)) });
        Assert.Equal(502, r.Status);
        Assert.Equal("GitHub refused the authorisation code", Assert.IsType<ApiError>(r.Body).Reason);
    }

    [Fact]
    public async Task The_connector_exchanges_the_code_and_lists_installations_without_the_operators()
    {
        var gh = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, """{"access_token":"ghu_user","token_type":"bearer"}"""),
            FakeGitHub.Json(HttpStatusCode.OK, """{"total_count":2,"installations":[{"id":42,"account":{"login":"ann-gh"}},{"id":99,"account":{"login":"jemmy8oy-northstar"}}]}"""));
        var connect = Connector(gh);

        var links = await connect.InstallationsAsync("c0de");

        Assert.Equal([(42L, "ann-gh")], links.Select(l => (l.Installation, l.Account)));
        Assert.Equal("https://github.example/login/oauth/access_token", gh.Sent[0].Uri.ToString());
        var form = HttpUtility.ParseQueryString(gh.Sent[0].Body!);
        Assert.Equal(("client-1", Secret, "c0de", "https://kit.example/kit/"), (form["client_id"], form["client_secret"], form["code"], form["redirect_uri"]));
        Assert.Equal("https://api.github.example/user/installations?per_page=100", gh.Sent[1].Uri.ToString());
        Assert.Equal("Bearer ghu_user", gh.Sent[1].Authorization);

        var authorize = new Uri(connect.AuthorizeUrl("1.ab"));
        var q = HttpUtility.ParseQueryString(authorize.Query);
        Assert.Equal(("client-1", "https://kit.example/kit/", "1.ab"), (q["client_id"], q["redirect_uri"], q["state"]));
    }

    [Fact]
    public async Task A_spent_code_and_a_GitHub_error_are_reasons_that_hold_no_secret()
    {
        var spent = Connector(new FakeGitHub(FakeGitHub.Json(HttpStatusCode.OK, """{"error":"bad_verification_code"}""")));
        var e = await Assert.ThrowsAsync<GitHubConnectException>(() => spent.InstallationsAsync("c0de"));
        Assert.Contains("used already or has expired", e.Message, StringComparison.Ordinal);

        var down = Connector(new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, """{"access_token":"ghu_user"}"""),
            FakeGitHub.Json(HttpStatusCode.Unauthorized, """{"message":"ghu_user the-client-secret"}""")));
        e = await Assert.ThrowsAsync<GitHubConnectException>(() => down.InstallationsAsync("c0de"));
        Assert.Equal("GitHub answered 401 when listing your installations", e.Message);

        foreach (var m in new[] { e.Message })
        {
            Assert.DoesNotContain(Secret, m, StringComparison.Ordinal);
            Assert.DoesNotContain("ghu_user", m, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_three_settings_switch_it_on_together_and_the_secret_never_prints()
    {
        var full = Env(
            ("KIT_PASSWORD", "pw"),
            ("KIT_DATABASE", "Host=db.invalid;Database=kit;Username=kit;Password=x"),
            ("KIT_PUBLIC_ORIGIN", "https://kit.example"),
            ("KIT_BASE_PATH", "/kit"),
            ("KIT_GITHUB_APP_SLUG", "kit"),
            ("KIT_GITHUB_CLIENT_ID", "client-1"),
            ("KIT_GITHUB_CLIENT_SECRET", Secret));
        var settings = KitSettings.FromEnvironment(full, RepoLayout.Root);

        Assert.True(settings.GitHubConnect);
        Assert.Equal("https://kit.example/kit/", settings.GitHubRedirect);
        Assert.DoesNotContain(Secret, settings.ToString(), StringComparison.Ordinal);
        Assert.NotNull(new ServiceCollection().AddKitServices(settings).BuildServiceProvider().GetService<IGitHubConnect>());

        var half = Assert.Throws<InvalidOperationException>(() => KitSettings.FromEnvironment(Without(full, "KIT_GITHUB_CLIENT_SECRET"), RepoLayout.Root));
        Assert.Contains("KIT_GITHUB_CLIENT_SECRET missing", half.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => KitSettings.FromEnvironment(Without(full, "KIT_DATABASE"), RepoLayout.Root));
        Assert.Throws<InvalidOperationException>(() => KitSettings.FromEnvironment(Without(full, "KIT_PUBLIC_ORIGIN"), RepoLayout.Root));

        var none = KitSettings.FromEnvironment(Without(Without(Without(full, "KIT_GITHUB_APP_SLUG"), "KIT_GITHUB_CLIENT_ID"), "KIT_GITHUB_CLIENT_SECRET"), RepoLayout.Root);
        Assert.False(none.GitHubConnect);
        Assert.Null(new ServiceCollection().AddKitServices(none).BuildServiceProvider().GetService<IGitHubConnect>());
    }

    private static GitHubConnect Connector(FakeGitHub gh) => new(
        new HttpClient(gh), "kit", "client-1", Secret, "https://kit.example/kit/", operatorInstallation: "99",
        web: new Uri("https://github.example/"), api: new Uri("https://api.github.example/"));

    private static AccountsTests.MemoryUsers Seeded()
    {
        var users = new AccountsTests.MemoryUsers();
        users.Add("ann", PasswordHash.Hash("ann-first-password", iterations: 1000));
        return users;
    }

    private static string SignIn(KitRouter router, string? name, string password = "ann-first-password")
    {
        var r = router.Route("POST", "/api/session", body: Json(name is null ? new { password } : (object)new { username = name, password }));
        Assert.Equal(200, r.Status);
        return r.SetCookie!.Split(';')[0];
    }

    private static SessionState Session(KitRouter router, string cookie) => Assert.IsType<SessionState>(router.Route("GET", "/api/session", cookie).Body);

    private static string StateOf(SessionState s) => HttpUtility.ParseQueryString(new Uri(s.GitHubAuthorize!).Query)["state"]!;

    private static IKitResponse Connect(KitRouter router, string cookie, object body) => router.Route("POST", "/api/github/connected", cookie, body: Json(body));

    private static string? Error(IKitResponse r) => (r.Body as ApiError)?.Error;

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static Func<string, string?> Env(params (string Key, string Value)[] pairs)
    {
        var env = pairs.ToDictionary(p => p.Key, p => p.Value);
        return k => env.GetValueOrDefault(k);
    }

    private static Func<string, string?> Without(Func<string, string?> env, string key) => k => k == key ? null : env(k);

    private static KitRouter Router(IUserStore users, IGitHubConnect? github, TimeProvider? clock = null)
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
            clock: clock,
            users: users,
            github: github);
    }

    /// <summary>GitHub, as far as the router sees it: the installations a code reaches.</summary>
    private sealed class Reachable(params IGitHubLink[] links) : IGitHubConnect
    {
        public int Asked { get; private set; }

        public string? Fails { get; init; }

        public string InstallUrl => "https://github.example/apps/kit/installations/new";

        public string AuthorizeUrl(string state) => $"https://github.example/login/oauth/authorize?client_id=c&state={Uri.EscapeDataString(state)}";

        public Task<IReadOnlyList<IGitHubLink>> InstallationsAsync(string code, CancellationToken cancellationToken = default)
        {
            Asked++;
            return Fails is { } why ? throw new GitHubConnectException(why) : Task.FromResult<IReadOnlyList<IGitHubLink>>(links);
        }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}
