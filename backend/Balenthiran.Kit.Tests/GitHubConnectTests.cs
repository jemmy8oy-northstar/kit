using System.Net;
using System.Text;
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
/// installing Kit's GitHub App on their own account. The installation recorded is the one on the
/// account that authorised: never the id the install redirect carries, which anyone can type, and
/// never an organisation's, which any member can reach.
/// </summary>
public class GitHubConnectTests
{
    private const string Operator = "operator-pw";
    private const string Secret = "the-client-secret";

    [Fact]
    public void A_user_connects_the_installation_on_their_own_account()
    {
        var users = Seeded();
        var router = Router(users, new Own(new GitHubLink { Installation = 42, Account = "ann-gh" }));
        var ann = SignIn(router, "ann");

        var before = Session(router, ann);
        Assert.Null(before.GitHub);
        Assert.Equal("https://github.example/apps/kit/installations/new", before.GitHubInstall);

        // A forged installation id in the body is not read at all.
        var r = Connect(router, ann, new { code = "c0de", state = StateOf(before), installation = 7 });
        Assert.Equal(200, r.Status);
        Assert.Equal("ann-gh", Assert.IsType<SessionState>(r.Body).GitHub);
        Assert.Equal(42, users.GitHubOf("ann")!.Installation);
        Assert.Equal("ann-gh", Session(router, ann).GitHub);
    }

    [Fact]
    public void No_installation_on_their_own_account_is_explained_and_nothing_is_stored()
    {
        var users = Seeded();
        var router = Router(users, new Own(null));
        var ann = SignIn(router, "ann");

        var r = Connect(router, ann, new { code = "c0de", state = StateOf(Session(router, ann)) });
        Assert.Equal(409, r.Status);
        Assert.Equal("no-installation", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Null(users.GitHubOf("ann"));
    }

    [Fact]
    public void A_state_made_for_another_user_or_gone_stale_is_refused_before_GitHub_is_asked()
    {
        var users = Seeded();
        users.Add("bob", PasswordHash.Hash("bob-first-password", iterations: 1000));
        var clock = new Clock(DateTimeOffset.Parse("2026-10-23T10:00:00Z"));
        var github = new Own(new GitHubLink { Installation = 42, Account = "ann-gh" });
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
        var router = Router(users, new Own(new GitHubLink { Installation = 42, Account = "ann-gh" }));

        var ann = SignIn(router, "ann");
        Assert.Equal(200, Connect(router, ann, new { code = "a", state = StateOf(Session(router, ann)) }).Status);

        var bob = SignIn(router, "bob", "bob-first-password");
        Assert.Equal("github-taken", Error(Connect(router, bob, new { code = "b", state = StateOf(Session(router, bob)) })));
        Assert.Null(users.GitHubOf("bob"));
    }

    [Fact]
    public void The_operator_is_offered_nothing_and_cannot_connect()
    {
        var router = Router(Seeded(), new Own(null));
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
        var router = Router(Seeded(), new Own(null) { Fails = "GitHub refused the authorisation code" });
        var ann = SignIn(router, "ann");
        var r = Connect(router, ann, new { code = "c", state = StateOf(Session(router, ann)) });
        Assert.Equal(502, r.Status);
        Assert.Equal("GitHub refused the authorisation code", Assert.IsType<ApiError>(r.Body).Reason);
    }

    [Fact]
    public void Once_connected_a_user_signs_in_with_GitHub_and_their_password_is_gone()
    {
        var users = Seeded();
        var router = Router(users, new Own(new GitHubLink { Installation = 42, Account = "ann-gh" }));
        var ann = SignIn(router, "ann");
        Assert.Null(Session(router, ann).GitHubSignIn);
        Assert.Equal(200, Connect(router, ann, new { code = "a", state = StateOf(Session(router, ann)) }).Status);

        // The right password, said back to someone who knew it, and only then.
        Assert.Equal("github-sign-in", Error(router.Route("POST", "/api/session", body: Json(new { username = "ann", password = "ann-first-password" }))));
        Assert.Equal("bad-password", Error(router.Route("POST", "/api/session", body: Json(new { username = "ann", password = "not-her-password" }))));
        Assert.Equal("github-sign-in", Error(router.Route("POST", "/api/session/password", ann, body: Json(new { current = "ann-first-password", next = "a-good-long-password" }))));

        var signedOut = Assert.IsType<SessionState>(router.Route("GET", "/api/session").Body);
        var state = HttpUtility.ParseQueryString(new Uri(signedOut.GitHubSignIn!).Query)["state"]!;
        Assert.StartsWith("in.", state, StringComparison.Ordinal);

        var r = GitHubSignIn(router, new { code = "b", state });
        Assert.Equal(200, r.Status);
        var cookie = r.SetCookie!.Split(';')[0];
        Assert.Equal("ann", Session(router, cookie).User);
        Assert.Null(Session(router, cookie).Operator);

        // His own sign-in is KIT_PASSWORD still.
        Assert.True(Session(router, SignIn(router, null, Operator)).Operator);
    }

    [Fact]
    public void A_GitHub_no_user_has_connected_signs_no_one_in()
    {
        foreach (var link in new IGitHubLink?[] { new GitHubLink { Installation = 43, Account = "stranger" }, null })
        {
            var router = Router(Seeded(), new Own(link));
            var r = GitHubSignIn(router, new { code = "c", state = SignInStateOf(router) });
            Assert.Equal(401, r.Status);
            Assert.Equal("github-not-connected", Error(r));
            Assert.Null(r.SetCookie);
        }
    }

    [Fact]
    public void A_connect_state_never_signs_in_and_a_sign_in_state_never_connects()
    {
        var users = Seeded();
        var clock = new Clock(DateTimeOffset.Parse("2026-10-27T15:00:00Z"));
        var github = new Own(new GitHubLink { Installation = 42, Account = "ann-gh" });
        var router = Router(users, github, clock);
        var ann = SignIn(router, "ann");

        var connect = StateOf(Session(router, ann));
        var signIn = SignInStateOf(router);
        Assert.Equal("bad-state", Error(GitHubSignIn(router, new { code = "c", state = connect })));
        Assert.Equal("bad-state", Error(GitHubSignIn(router, new { code = "c", state = "in." + connect })));
        Assert.Equal("bad-state", Error(Connect(router, ann, new { code = "c", state = signIn })));
        Assert.Equal("bad-state", Error(GitHubSignIn(router, new { code = "c" })));

        clock.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal("bad-state", Error(GitHubSignIn(router, new { code = "c", state = signIn })));
        Assert.Equal(0, github.Asked);
    }

    [Fact]
    public void Without_a_connector_there_is_no_GitHub_sign_in_and_the_password_still_works()
    {
        var users = Seeded();
        users.SetGitHub("ann", new GitHubLink { Installation = 42, Account = "ann-gh" });
        var router = Router(users, github: null);

        Assert.Null(Assert.IsType<SessionState>(router.Route("GET", "/api/session").Body).GitHubSignIn);
        Assert.Equal(404, GitHubSignIn(router, new { code = "c", state = "in.1.ab" }).Status);
        Assert.Equal("ann", Session(router, SignIn(router, "ann")).User);
    }

    [Fact]
    public async Task The_connector_takes_only_the_authorising_accounts_installation_and_revokes_the_token()
    {
        // GitHub lists every installation ann can REACH: an organisation she is a member of comes first.
        var gh = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, """{"access_token":"ghu_user","token_type":"bearer"}"""),
            FakeGitHub.Json(HttpStatusCode.OK, """{"login":"Ann-GH"}"""),
            FakeGitHub.Json(HttpStatusCode.OK, """{"total_count":2,"installations":[{"id":43,"account":{"login":"some-org"}},{"id":42,"account":{"login":"ann-gh"}}]}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var connect = Connector(gh);

        var link = await connect.InstallationAsync("c0de");

        Assert.Equal((42L, "Ann-GH"), (link!.Installation, link.Account));
        Assert.Equal("https://github.example/login/oauth/access_token", gh.Sent[0].Uri.ToString());
        var form = HttpUtility.ParseQueryString(gh.Sent[0].Body!);
        Assert.Equal(("client-1", Secret, "c0de", "https://kit.example/kit/"), (form["client_id"], form["client_secret"], form["code"], form["redirect_uri"]));
        Assert.Equal(("https://api.github.example/user", "Bearer ghu_user"), (gh.Sent[1].Uri.ToString(), gh.Sent[1].Authorization));
        Assert.Equal(("https://api.github.example/user/installations?per_page=100", "Bearer ghu_user"), (gh.Sent[2].Uri.ToString(), gh.Sent[2].Authorization));

        // Revoked with the App's own client credential, so the user token outlives nothing.
        Assert.Equal((HttpMethod.Delete, "https://api.github.example/applications/client-1/token"), (gh.Sent[3].Method, gh.Sent[3].Uri.ToString()));
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"client-1:{Secret}")), gh.Sent[3].Authorization);
        Assert.Equal("ghu_user", JsonDocument.Parse(gh.Sent[3].Body!).RootElement.GetProperty("access_token").GetString());

        var q = HttpUtility.ParseQueryString(new Uri(connect.AuthorizeUrl("1.ab")).Query);
        Assert.Equal(("client-1", "https://kit.example/kit/", "1.ab"), (q["client_id"], q["redirect_uri"], q["state"]));
    }

    [Fact]
    public async Task An_organisations_or_the_operators_installation_is_never_returned()
    {
        foreach (var installations in new[]
        {
            """{"installations":[{"id":43,"account":{"login":"some-org"}}]}""",
            """{"installations":[{"id":99,"account":{"login":"ann-gh"}}]}""",
            """{"installations":[]}""",
        })
        {
            var gh = new FakeGitHub(
                FakeGitHub.Json(HttpStatusCode.OK, """{"access_token":"ghu_user"}"""),
                FakeGitHub.Json(HttpStatusCode.OK, """{"login":"ann-gh"}"""),
                FakeGitHub.Json(HttpStatusCode.OK, installations),
                _ => new HttpResponseMessage(HttpStatusCode.NoContent));

            Assert.Null(await Connector(gh).InstallationAsync("c0de"));
            Assert.Equal(HttpMethod.Delete, gh.Sent[^1].Method);
        }
    }

    [Fact]
    public async Task A_spent_code_and_a_GitHub_error_are_reasons_that_hold_no_secret()
    {
        var spent = new FakeGitHub(FakeGitHub.Json(HttpStatusCode.OK, """{"error":"bad_verification_code"}"""));
        var e = await Assert.ThrowsAsync<GitHubConnectException>(() => Connector(spent).InstallationAsync("c0de"));
        Assert.Contains("used already or has expired", e.Message, StringComparison.Ordinal);
        Assert.Single(spent.Sent);

        // The token is revoked even when the question after the exchange fails, and a failed
        // revoke changes nothing.
        var down = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, """{"access_token":"ghu_user"}"""),
            FakeGitHub.Json(HttpStatusCode.Unauthorized, """{"message":"ghu_user the-client-secret"}"""),
            _ => throw new HttpRequestException("revoke failed"));
        e = await Assert.ThrowsAsync<GitHubConnectException>(() => Connector(down).InstallationAsync("c0de"));
        Assert.Equal("GitHub answered 401 when reading who you are", e.Message);
        Assert.Equal(HttpMethod.Delete, down.Sent[^1].Method);
        Assert.DoesNotContain(Secret, e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ghu_user", e.Message, StringComparison.Ordinal);
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

    private static IKitResponse GitHubSignIn(KitRouter router, object body) => router.Route("POST", "/api/session/github", body: Json(body));

    private static string SignInStateOf(KitRouter router) =>
        HttpUtility.ParseQueryString(new Uri(Assert.IsType<SessionState>(router.Route("GET", "/api/session").Body).GitHubSignIn!).Query)["state"]!;

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

    /// <summary>GitHub, as far as the router sees it: the installation on the authorising account, if any.</summary>
    private sealed class Own(IGitHubLink? link) : IGitHubConnect
    {
        public int Asked { get; private set; }

        public string? Fails { get; init; }

        public string InstallUrl => "https://github.example/apps/kit/installations/new";

        public string AuthorizeUrl(string state) => $"https://github.example/login/oauth/authorize?client_id=c&state={Uri.EscapeDataString(state)}";

        public Task<IGitHubLink?> InstallationAsync(string code, CancellationToken cancellationToken = default)
        {
            Asked++;
            return Fails is { } why ? throw new GitHubConnectException(why) : Task.FromResult(link);
        }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}
