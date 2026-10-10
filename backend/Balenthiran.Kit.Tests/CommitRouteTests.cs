using System.Net;
using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit#155 slice 2: <c>POST /api/commit</c> proposes the edits branch to <c>dev</c> as one pull
/// request. It has no <c>ui.js</c> counterpart, so there is no golden: what is scored here is that
/// it sits behind the same gates as every edit, refuses with a sentence when it cannot propose,
/// and that the registered server hands the opener the configured token, repository and base.
/// </summary>
public class CommitRouteTests
{
    private const string Token = "ghp_SECRET_never_shown";

    private static readonly EngineJsonSerialiser Serialiser = new();

    [Fact]
    public void Opens_one_pull_request_from_the_edits_branch_into_the_base()
    {
        var pulls = new ScriptedOpener(new PullRequestResult { Opened = true, Number = 13, Url = "https://github.com/o/kit/pull/13" });

        var r = Router(pulls).Route("POST", "/api/commit");

        Assert.Equal(200, r.Status);
        var body = Body(r);
        Assert.True(body.GetProperty("opened").GetBoolean());
        Assert.Equal(13, body.GetProperty("number").GetInt32());
        Assert.Equal("https://github.com/o/kit/pull/13", body.GetProperty("url").GetString());
        var call = Assert.Single(pulls.Calls);
        Assert.Equal(("kit/hosted", "dev"), (call.Head, call.Base));
    }

    [Fact]
    public void A_second_press_reports_the_one_already_open()
    {
        var pulls = new ScriptedOpener(new PullRequestResult { AlreadyOpen = true, Number = 12, Url = "https://github.com/o/kit/pull/12" });

        var body = Body(Router(pulls).Route("POST", "/api/commit"));

        Assert.True(body.GetProperty("alreadyOpen").GetBoolean());
        Assert.False(body.GetProperty("opened").GetBoolean());
    }

    [Fact]
    public void GitHubs_refusal_is_a_409_in_its_own_words()
    {
        var pulls = new ScriptedOpener(new PullRequestResult { Reason = "GitHub answered 422: Validation Failed — No commits between dev and kit/hosted" });

        var r = Router(pulls).Route("POST", "/api/commit");

        Assert.Equal(409, r.Status);
        Assert.Equal("no-pull-request", Body(r).GetProperty("error").GetString());
        Assert.Equal("GitHub answered 422: Validation Failed — No commits between dev and kit/hosted", Body(r).GetProperty("reason").GetString());
    }

    [Fact]
    public void With_write_back_off_there_is_nothing_to_propose_and_GitHub_is_not_asked()
    {
        var pulls = new ScriptedOpener();

        var r = Router(pulls, git: false).Route("POST", "/api/commit");

        Assert.Equal(409, r.Status);
        Assert.Equal("git-off", Body(r).GetProperty("error").GetString());
        Assert.Empty(pulls.Calls);
    }

    [Fact]
    public void With_no_edits_branch_named_it_refuses_rather_than_guess()
    {
        var pulls = new ScriptedOpener();

        var r = Router(pulls, head: null).Route("POST", "/api/commit");

        Assert.Equal(409, r.Status);
        Assert.Equal("no-edits-branch", Body(r).GetProperty("error").GetString());
        Assert.Empty(pulls.Calls);
    }

    [Fact]
    public void Locked_it_needs_a_session_like_every_edit()
    {
        var pulls = new ScriptedOpener(new PullRequestResult { Opened = true, Number = 1 });
        var router = Router(pulls, password: "pw");

        var refused = router.Route("POST", "/api/commit");
        Assert.Equal(401, refused.Status);
        Assert.Empty(pulls.Calls);

        var signIn = router.Route("POST", "/api/session", body: JsonDocument.Parse("""{"password":"pw"}""").RootElement);
        var cookie = signIn.SetCookie!.Split(';')[0];
        Assert.Equal(200, router.Route("POST", "/api/commit", cookie).Status);
        Assert.Single(pulls.Calls);
    }

    [Fact]
    public void A_cross_origin_press_is_refused_before_GitHub_is_asked()
    {
        var pulls = new ScriptedOpener(new PullRequestResult { Opened = true, Number = 1 });

        var r = Router(pulls, publicOrigin: "https://balenthiran.co.uk").Route("POST", "/api/commit", origin: "https://balenthiran.co.uk.evil.com");

        Assert.Equal(403, r.Status);
        Assert.Empty(pulls.Calls);
    }

    /// <summary>
    /// kit#160's blind review: Kestrel serves presses concurrently, so two at once both found no
    /// open pull request and both created one. Against a GitHub that is slow to create, one
    /// press opens it and the other must find it.
    /// </summary>
    [Fact]
    public async Task Two_presses_at_once_open_ONE_pull_request()
    {
        var github = new SlowGitHub();
        var raced = new KitRouter(
            new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot),
            new ProjectViewer(new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot), new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter()),
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            null,
            git: new GitStore(enabled: true),
            pulls: github,
            head: "kit/hosted");

        var both = await Task.WhenAll(Task.Run(() => raced.Route("POST", "/api/commit")), Task.Run(() => raced.Route("POST", "/api/commit")));

        Assert.All(both, r => Assert.Equal(200, r.Status));
        Assert.Equal(1, github.Created);
        Assert.Single(both, r => Body(r).GetProperty("alreadyOpen").GetBoolean());
    }

    /// <summary>
    /// The whole chain from the environment the chart sets to the request GitHub receives —
    /// the router tests above build their router by hand, so they cannot see the registration
    /// drop the token, the repository or the base.
    /// </summary>
    [Fact]
    public async Task The_registered_server_asks_GitHub_with_the_configured_token_repository_and_base()
    {
        var env = new Dictionary<string, string?>
        {
            ["KIT_GIT"] = "1",
            ["KIT_GIT_BRANCH"] = "kit/hosted",
            ["KIT_GIT_BASE"] = "main",
            ["KIT_GIT_TOKEN"] = Token,
            ["KIT_GIT_CLONE"] = "https://github.com/jemmy8oy-northstar/kit.git",
        };
        var settings = KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root);
        var gh = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, "[]"),
            FakeGitHub.Json(HttpStatusCode.Created, """{"number":7,"html_url":"https://github.com/jemmy8oy-northstar/kit/pull/7"}"""));
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], settings, s => s.AddSingleton<HttpMessageHandler>(gh));
        var host = app.Services.GetRequiredService<IKitHost>();

        var a = host.Received("/api/commit", Encoding.UTF8.GetBytes("{}"), null, null);

        Assert.Equal(200, a.Status);
        Assert.Equal(2, gh.Sent.Count);
        Assert.Equal(
            "https://api.github.com/repos/jemmy8oy-northstar/kit/pulls?state=open&head=jemmy8oy-northstar%3Akit%2Fhosted&base=main",
            gh.Sent[0].Uri.AbsoluteUri);
        Assert.Equal($"Bearer {Token}", gh.Sent[0].Authorization);
        using var sent = JsonDocument.Parse(gh.Sent[1].Body!);
        Assert.Equal("kit/hosted", sent.RootElement.GetProperty("head").GetString());
        Assert.Equal("main", sent.RootElement.GetProperty("base").GetString());
        Assert.DoesNotContain(Token, a.Body);
    }

    [Fact]
    public void The_base_defaults_to_dev_and_the_settings_never_print_the_token()
    {
        var env = new Dictionary<string, string?> { ["KIT_GIT_TOKEN"] = Token, ["KIT_GIT_CLONE"] = "git@github.com:jemmy8oy-northstar/kit.git" };

        var settings = KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root);

        Assert.Equal("dev", settings.GitBase);
        Assert.Equal("jemmy8oy-northstar/kit", settings.GitRepository);
        Assert.DoesNotContain(Token, settings.ToString());
    }

    private static KitRouter Router(ScriptedOpener pulls, bool git = true, string? head = "kit/hosted", string? password = null, string? publicOrigin = null)
    {
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        return new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            password,
            new OriginPolicy(new UrlParser(), publicOrigin),
            new SessionStore(),
            new SignInThrottle(),
            git: new GitStore(enabled: git),
            pulls: pulls,
            head: head);
    }

    private static JsonElement Body(IKitResponse r) => JsonDocument.Parse(Serialiser.Serialise(r.Body!)).RootElement;

    /// <summary>GitHub's find-then-create, with the create slow enough that two unserialised presses overlap.</summary>
    private sealed class SlowGitHub : IPullRequestOpener
    {
        private int? open;

        public int Created { get; private set; }

        public async Task<IPullRequestResult> OpenAsync(string head, string baseBranch, string title, string body, CancellationToken cancellationToken = default)
        {
            if (open is { } n)
            {
                return new PullRequestResult { AlreadyOpen = true, Number = n };
            }

            await Task.Delay(200, cancellationToken);
            Created++;
            open = 13;
            return new PullRequestResult { Opened = true, Number = 13 };
        }
    }

    /// <summary>Answers from a script and records what it was asked, so a test can assert GitHub was NOT asked.</summary>
    private sealed class ScriptedOpener(params IPullRequestResult[] answers) : IPullRequestOpener
    {
        public List<(string Head, string Base)> Calls { get; } = [];

        public Task<IPullRequestResult> OpenAsync(string head, string baseBranch, string title, string body, CancellationToken cancellationToken = default)
        {
            Calls.Add((head, baseBranch));
            return Task.FromResult(answers[Calls.Count - 1]);
        }
    }
}
