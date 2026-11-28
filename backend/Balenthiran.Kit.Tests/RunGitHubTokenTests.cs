using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-USERS-9</c> slice D2: a run carries no GitHub token. Each time git needs one the runner asks
/// Kit with its run token, and Kit mints an installation token for that ONE repo with contents:write and
/// pull_requests:write only. GitHub is scripted, so what Kit asked it for is asserted, not assumed.
/// </summary>
public class RunGitHubTokenTests
{
    private const string PoolToken = "pool-secret-do-not-print";
    private const string Operator = "operator-pw";
    private const string Minted = "ghs_repo_scoped_never_shown";
    private static readonly RSA Key = RSA.Create(2048);

    private sealed class Rig
    {
        private readonly GitHubAppTokenSource? app;
        private readonly TimeProvider? clock;

        public Rig(FakeGitHub gh, bool withApp = true, TimeProvider? clock = null)
        {
            this.clock = clock;
            Github = gh;
            Store = new MemoryAgentRunStore();
            Pool = new RunnerPool(Store);
            var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            Router = new KitRouter(
                corpora,
                viewer,
                new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
                Operator,
                new OriginPolicy(new UrlParser(), null),
                new SessionStore(),
                new SignInThrottle(),
                runStore: Store,
                runLauncher: Pool,
                runnerPool: Pool);
            app = withApp ? new GitHubAppTokenSource(new HttpClient(gh), "123456", "7890", Key.ExportRSAPrivateKeyPem()) : null;
            Routes = new RunnerPoolRoutes(Pool, Store, PoolToken, TimeSpan.Zero, clock, app);
            var signIn = Router.Route("POST", "/api/session", body: Json(new { password = Operator }));
            Cookie = signIn.SetCookie!.Split(';')[0];
        }

        public FakeGitHub Github { get; }

        public MemoryAgentRunStore Store { get; }

        public RunnerPool Pool { get; private set; }

        public KitRouter Router { get; }

        public RunnerPoolRoutes Routes { get; private set; }

        public string Cookie { get; }

        public AgentRunStarted Start(string project, string repo)
        {
            var r = Router.Route("POST", $"/api/projects/{project}/runs", Cookie, body: Json(new { repo, task = "do the thing" }));
            return Assert.IsType<AgentRunStarted>(r.Body);
        }

        public AgentRunStarted StartAndClaim(string project, string repo)
        {
            var started = Start(project, repo);
            Assert.Equal(200, Routes.ClaimAsync(PoolToken).GetAwaiter().GetResult().Status);
            return started;
        }

        /// <summary>What a Kit restart does to the pool: a new one, with nothing in memory, over the same store.</summary>
        public void Restart()
        {
            Pool = new RunnerPool(Store);
            Routes = new RunnerPoolRoutes(Pool, Store, PoolToken, TimeSpan.Zero, clock, app);
        }

        public KitResponse Ask(string? runToken) => Routes.GitHubTokenAsync(runToken).GetAwaiter().GetResult();
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static Func<HttpRequestMessage, HttpResponseMessage> Grant() =>
        FakeGitHub.Json(HttpStatusCode.Created, JsonSerializer.Serialize(new { token = Minted, expires_at = DateTimeOffset.UtcNow.AddHours(1).ToString("O") }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kitrun_nope")]
    public void An_unknown_or_missing_run_token_is_401_and_GitHub_is_not_asked(string? token)
    {
        var rig = new Rig(new FakeGitHub());

        var r = rig.Ask(token);

        Assert.Equal(401, r.Status);
        Assert.Equal("not-a-run", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Empty(rig.Github.Sent);
    }

    [Fact]
    public void A_queued_run_is_403_and_GitHub_is_not_asked()
    {
        var rig = new Rig(new FakeGitHub());
        var started = rig.Start("kit", "octo/cat");

        var r = rig.Ask(started.Token);

        Assert.Equal(403, r.Status);
        Assert.Equal("run-not-running", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Empty(rig.Github.Sent);
    }

    [Theory]
    [InlineData("finished")]
    [InlineData("failed")]
    public void A_run_that_is_over_is_403_and_GitHub_is_not_asked(string status)
    {
        var rig = new Rig(new FakeGitHub());
        var started = rig.StartAndClaim("kit", "octo/cat");
        Assert.Equal(200, rig.Routes.Finished(started.Token, Json(new { status })).Status);

        var r = rig.Ask(started.Token);

        Assert.Equal(403, r.Status);
        Assert.Equal("run-not-running", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Empty(rig.Github.Sent);
    }

    [Fact]
    public void A_run_past_its_maximum_age_is_403()
    {
        var gh = new FakeGitHub();
        var late = new Late(DateTimeOffset.UtcNow.AddHours(7));
        var rig = new Rig(gh, clock: late);
        var started = rig.StartAndClaim("kit", "octo/cat");

        var r = rig.Ask(started.Token);

        Assert.Equal(403, r.Status);
        Assert.Equal("run-too-old", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Empty(gh.Sent);
    }

    [Fact]
    public void A_running_run_gets_a_token_minted_for_that_one_repo_with_contents_and_pull_requests_write_only()
    {
        var gh = new FakeGitHub(Grant());
        var rig = new Rig(gh);
        var started = rig.StartAndClaim("kit", "octo/cat");

        var r = rig.Ask(started.Token);

        Assert.Equal(200, r.Status);
        Assert.Equal("no-store", r.CacheControl);
        var grant = Assert.IsType<GitHubTokenGrant>(r.Body);
        Assert.Equal(Minted, grant.Token);
        Assert.True(grant.ExpiresAt > DateTimeOffset.UtcNow);

        var sent = Assert.Single(gh.Sent);
        Assert.Equal("https://api.github.com/app/installations/7890/access_tokens", sent.Uri.AbsoluteUri);
        using var body = JsonDocument.Parse(sent.Body!);
        var root = body.RootElement;
        Assert.Equal(["cat"], root.GetProperty("repositories").EnumerateArray().Select(e => e.GetString()!).ToArray());
        var permissions = root.GetProperty("permissions").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
        Assert.Equal(new Dictionary<string, string?> { ["contents"] = "write", ["pull_requests"] = "write" }, permissions);
    }

    [Fact]
    public void Each_ask_mints_again_and_a_run_can_only_reach_its_own_repo()
    {
        var gh = new FakeGitHub(Grant(), Grant());
        var rig = new Rig(gh);
        var mine = rig.StartAndClaim("kit", "octo/mine");
        var theirs = rig.StartAndClaim("kit-ui", "octo/theirs");

        Assert.Equal(200, rig.Ask(mine.Token).Status);
        Assert.Equal(200, rig.Ask(theirs.Token).Status);

        Assert.Equal(2, gh.Sent.Count);
        Assert.Contains("\"repositories\":[\"mine\"]", gh.Sent[0].Body);
        Assert.DoesNotContain("theirs", gh.Sent[0].Body);
        Assert.Contains("\"repositories\":[\"theirs\"]", gh.Sent[1].Body);
        Assert.DoesNotContain("mine", gh.Sent[1].Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public void An_installation_that_does_not_reach_the_repo_is_a_403_not_a_500(HttpStatusCode github)
    {
        var rig = new Rig(new FakeGitHub(FakeGitHub.Json(github, """{"message":"nope"}""")));
        var started = rig.StartAndClaim("kit", "octo/cat");

        var r = rig.Ask(started.Token);

        Assert.Equal(403, r.Status);
        var error = Assert.IsType<ApiError>(r.Body);
        Assert.Equal("repo-not-installed", error.Error);
        Assert.Contains("octo/cat", error.Reason);
        Assert.Contains("Pull requests", error.Reason);
    }

    [Fact]
    public void A_GitHub_failure_is_a_502_that_does_not_echo_what_GitHub_said()
    {
        var rig = new Rig(new FakeGitHub(FakeGitHub.Json(HttpStatusCode.InternalServerError, """{"message":"secret-detail"}""")));
        var started = rig.StartAndClaim("kit", "octo/cat");

        var r = rig.Ask(started.Token);

        Assert.Equal(502, r.Status);
        Assert.DoesNotContain("secret-detail", Assert.IsType<ApiError>(r.Body).Reason);
    }

    [Fact]
    public void A_Kit_with_no_GitHub_App_answers_503()
    {
        var rig = new Rig(new FakeGitHub(), withApp: false);
        var started = rig.StartAndClaim("kit", "octo/cat");

        var r = rig.Ask(started.Token);

        Assert.Equal(503, r.Status);
        Assert.Equal("no-github-app", Assert.IsType<ApiError>(r.Body).Error);
    }

    [Fact]
    public void A_run_this_process_never_handed_out_is_403_because_its_repo_is_unknown()
    {
        var rig = new Rig(new FakeGitHub());
        var started = rig.Start("kit", "octo/cat");
        rig.Store.StartIfQueued(started.Run.Id);

        var r = rig.Ask(started.Token);

        Assert.Equal(403, r.Status);
        Assert.Equal("repo-unknown", Assert.IsType<ApiError>(r.Body).Error);
        Assert.Empty(rig.Github.Sent);
    }

    [Fact]
    public void BEH_ORCH_8_a_run_handed_out_before_a_restart_still_gets_a_token_for_its_one_repo()
    {
        var gh = new FakeGitHub(Grant());
        var rig = new Rig(gh);
        var started = rig.StartAndClaim("kit", "octo/cat");

        rig.Restart();
        var r = rig.Ask(started.Token);

        Assert.Equal(200, r.Status);
        Assert.Equal(Minted, Assert.IsType<GitHubTokenGrant>(r.Body).Token);
        var sent = Assert.Single(gh.Sent);
        Assert.Contains("\"repositories\":[\"cat\"]", sent.Body);
    }

    [Fact]
    public void BEH_ORCH_8_after_a_restart_a_run_that_is_over_reaches_no_repo()
    {
        var rig = new Rig(new FakeGitHub(Grant()));
        var started = rig.StartAndClaim("kit", "octo/cat");
        Assert.Equal(200, rig.Routes.Finished(started.Token, Json(new { status = "finished" })).Status);

        rig.Restart();

        Assert.Null(rig.Pool.RepoOf(started.Run.Id));
        Assert.Equal("run-not-running", Assert.IsType<ApiError>(rig.Ask(started.Token).Body).Error);
        Assert.Empty(rig.Github.Sent);
    }

    [Fact]
    public void BEH_ORCH_8_a_run_keeps_the_repo_it_started_with_and_a_second_start_cannot_change_it()
    {
        var store = new MemoryAgentRunStore();
        var run = store.Create("o", "kit", "m", "hash", DateTimeOffset.UtcNow);
        Assert.Null(store.RepoOf(run.Id));

        Assert.True(store.StartIfQueued(run.Id, "octo/cat"));
        Assert.False(store.StartIfQueued(run.Id, "octo/other"));

        Assert.Equal("octo/cat", store.RepoOf(run.Id));
        Assert.Null(store.RepoOf("no-such-run"));
    }

    [Fact]
    public void Neither_the_listing_nor_the_store_ever_holds_a_minted_token()
    {
        var rig = new Rig(new FakeGitHub(Grant()));
        var started = rig.StartAndClaim("kit", "octo/cat");
        Assert.Equal(200, rig.Ask(started.Token).Status);

        var listed = JsonSerializer.Serialize(rig.Router.Route("GET", "/api/runs", rig.Cookie).Body, typeof(AgentRunList), new JsonSerializerOptions());
        // The store keeps the run's repo (BEH-ORCH-8), but apart from the run, so what it lists never shows it.
        var stored = JsonSerializer.Serialize(rig.Store.List(), new JsonSerializerOptions());
        foreach (var text in new[] { listed, stored })
        {
            Assert.DoesNotContain(Minted, text, StringComparison.Ordinal);
            Assert.DoesNotContain("octo/cat", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_grant_does_not_print_its_token()
    {
        var grant = new GitHubTokenGrant(Minted, DateTimeOffset.UtcNow);

        Assert.DoesNotContain(Minted, grant.ToString(), StringComparison.Ordinal);
    }

    private sealed class Late(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
