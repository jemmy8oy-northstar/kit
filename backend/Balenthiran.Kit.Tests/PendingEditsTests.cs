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
/// BEH-COMMIT-5: the project page lists the edits that have not reached dev yet, and the pull
/// request they wait in. The reader is scored against a scripted GitHub (which requests, what is
/// read back from Kit's own commit messages); the route against a scripted reader (one project's
/// share, and "off" rendering as nothing rather than as an error).
/// </summary>
public class PendingEditsTests
{
    private const string Token = "ghp_SECRET_never_shown";
    private const string Repo = "jemmy8oy-northstar/kit";

    private static readonly EngineJsonSerialiser Serialiser = new();

    // GitHub's compare lists commits OLDEST first.
    private const string Compare = """
        {"ahead_by":3,"commits":[
          {"sha":"aaaaaaa1111111","commit":{"message":"kit: add BEH-7 (snip-it)","committer":{"date":"2026-10-13T10:00:00Z"}}},
          {"sha":"bbbbbbb2222222","commit":{"message":"kit: note (kit)\n\nbody line","committer":{"date":"2026-10-13T11:00:00Z"}}},
          {"sha":"ccccccc3333333","commit":{"message":"kit: adjudicate BEH-ORDER-1 (snip-it)","committer":{"date":"2026-10-13T12:00:00Z"}}}
        ]}
        """;

    private static GitHubPullRequestOpener Reader(FakeGitHub gh, string? token = Token) =>
        new(new HttpClient(gh), new StaticGitHubTokenSource(token), Repo);

    [Fact]
    public async Task The_edits_branch_is_read_newest_first_with_the_pull_request_it_waits_in()
    {
        var gh = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, Compare),
            FakeGitHub.Json(HttpStatusCode.OK, """[{"number":12,"html_url":"https://github.com/jemmy8oy-northstar/kit/pull/12"}]"""));

        var r = await Reader(gh).PendingAsync("kit/hosted", "dev");

        Assert.Null(r.Reason);
        Assert.Equal(["ccccccc", "bbbbbbb", "aaaaaaa"], r.Edits.Select(e => e.Sha));
        var newest = r.Edits[0];
        Assert.Equal(("adjudicate BEH-ORDER-1", "snip-it", "BEH-ORDER-1", "2026-10-13T12:00:00Z"), (newest.Summary, newest.App, newest.Behaviour, newest.When));
        // A note names no behaviour, and only the message's first line is kept.
        Assert.Equal(("note", "kit", (string?)null), (r.Edits[1].Summary, r.Edits[1].App, r.Edits[1].Behaviour));
        Assert.Equal((12, "https://github.com/jemmy8oy-northstar/kit/pull/12"), (r.PullNumber, r.PullUrl));
        // The branch reaches GitHub as a path with its slash, not as %2F.
        Assert.Equal("https://api.github.com/repos/jemmy8oy-northstar/kit/compare/dev...kit/hosted", gh.Sent[0].Uri.AbsoluteUri);
        Assert.Equal(
            "https://api.github.com/repos/jemmy8oy-northstar/kit/pulls?state=open&head=jemmy8oy-northstar%3Akit%2Fhosted&base=dev",
            gh.Sent[1].Uri.AbsoluteUri);
        Assert.All(gh.Sent, s => Assert.Equal($"Bearer {Token}", s.Authorization));
    }

    [Fact]
    public async Task An_edits_branch_nobody_has_pushed_yet_is_nothing_pending_not_a_failure()
    {
        var gh = new FakeGitHub(FakeGitHub.Json(HttpStatusCode.NotFound, """{"message":"Not Found"}"""));

        var r = await Reader(gh).PendingAsync("kit/hosted", "dev");

        Assert.Empty(r.Edits);
        Assert.Null(r.Reason);
        Assert.Single(gh.Sent);
    }

    [Fact]
    public async Task Nothing_ahead_does_not_ask_for_a_pull_request()
    {
        var gh = new FakeGitHub(FakeGitHub.Json(HttpStatusCode.OK, """{"ahead_by":0,"commits":[]}"""));

        var r = await Reader(gh).PendingAsync("kit/hosted", "dev");

        Assert.Empty(r.Edits);
        Assert.Null(r.PullNumber);
        Assert.Single(gh.Sent);
    }

    [Fact]
    public async Task A_commit_Kit_did_not_write_keeps_its_first_line_and_belongs_to_no_project()
    {
        var gh = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, """{"commits":[{"sha":"ddddddd4444444","commit":{"message":"Merge branch 'dev' into kit/hosted"}}]}"""),
            FakeGitHub.Json(HttpStatusCode.OK, "[]"));

        var edit = Assert.Single((await Reader(gh).PendingAsync("kit/hosted", "dev")).Edits);

        Assert.Equal(("Merge branch 'dev' into kit/hosted", (string?)null, (string?)null), (edit.Summary, edit.App, edit.When));
    }

    [Fact]
    public async Task GitHubs_refusal_is_a_reason_and_never_carries_the_token()
    {
        var gh = new FakeGitHub(FakeGitHub.Json(HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}"""));

        var r = await Reader(gh).PendingAsync("kit/hosted", "dev");

        Assert.Empty(r.Edits);
        Assert.Equal("GitHub answered 401: Bad credentials", r.Reason);
        Assert.DoesNotContain(Token, r.Reason);
    }

    [Fact]
    public async Task With_no_token_GitHub_is_not_asked()
    {
        var gh = new FakeGitHub();

        var r = await Reader(gh, token: null).PendingAsync("kit/hosted", "dev");

        Assert.NotNull(r.Reason);
        Assert.Empty(gh.Sent);
    }

    [Fact]
    public void The_route_lists_only_this_projects_edits_beside_the_pull_request()
    {
        var reader = new ScriptedReader(new PendingEdits
        {
            Edits =
            [
                new PendingEdit { Sha = "ccccccc", Summary = "adjudicate BEH-ORDER-1", App = "snip-it", Behaviour = "BEH-ORDER-1" },
                new PendingEdit { Sha = "bbbbbbb", Summary = "note", App = "kit" },
            ],
            PullNumber = 12,
            PullUrl = "https://github.com/o/kit/pull/12",
        });

        var r = Router(reader).Route("GET", "/api/projects/snip-it/pending");

        Assert.Equal(200, r.Status);
        var body = Body(r);
        var only = Assert.Single(body.GetProperty("edits").EnumerateArray());
        Assert.Equal("BEH-ORDER-1", only.GetProperty("behaviour").GetString());
        Assert.Equal(12, body.GetProperty("pullNumber").GetInt32());
        Assert.Equal(("kit/hosted", "dev"), (body.GetProperty("head").GetString(), body.GetProperty("base").GetString()));
        Assert.Equal(("kit/hosted", "dev"), Assert.Single(reader.Calls));
    }

    [Fact]
    public void A_project_with_nothing_pending_is_not_shown_another_projects_pull_request()
    {
        var reader = new ScriptedReader(new PendingEdits
        {
            Edits = [new PendingEdit { Sha = "bbbbbbb", Summary = "note", App = "kit" }],
            PullNumber = 12,
            PullUrl = "https://github.com/o/kit/pull/12",
        });

        var body = Body(Router(reader).Route("GET", "/api/projects/snip-it/pending"));

        Assert.Empty(body.GetProperty("edits").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("pullNumber").ValueKind);
    }

    [Fact]
    public void With_write_back_off_it_lists_nothing_and_GitHub_is_not_asked()
    {
        var reader = new ScriptedReader();

        var r = Router(reader, git: false).Route("GET", "/api/projects/snip-it/pending");

        Assert.Equal(200, r.Status);
        Assert.Empty(Body(r).GetProperty("edits").EnumerateArray());
        Assert.Empty(reader.Calls);
    }

    [Fact]
    public void It_is_a_read_so_a_locked_Kit_serves_it_without_a_session()
    {
        var reader = new ScriptedReader(new PendingEdits());

        Assert.Equal(200, Router(reader, password: "pw").Route("GET", "/api/projects/snip-it/pending").Status);
    }

    [Fact]
    public void An_unknown_project_is_a_404_and_GitHub_is_not_asked()
    {
        var reader = new ScriptedReader();

        var r = Router(reader).Route("GET", "/api/projects/nope/pending");

        Assert.Equal(404, r.Status);
        Assert.Equal("no-such-project", Body(r).GetProperty("error").GetString());
        Assert.Empty(reader.Calls);
    }

    /// <summary>The registration hands the reader the configured token, repository and branches.</summary>
    [Fact]
    public async Task The_registered_server_compares_the_configured_base_and_edits_branch()
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
        var gh = new FakeGitHub(FakeGitHub.Json(HttpStatusCode.OK, """{"commits":[]}"""));
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], settings, s => s.AddSingleton<HttpMessageHandler>(gh));
        var router = app.Services.GetRequiredService<IKitRouter>();

        var known = app.Services.GetRequiredService<ICorpusDirectory>().Corpora()[0];
        var r = router.Route("GET", $"/api/projects/{known}/pending");

        Assert.Equal(200, r.Status);
        Assert.Equal("https://api.github.com/repos/jemmy8oy-northstar/kit/compare/main...kit/hosted", Assert.Single(gh.Sent).Uri.AbsoluteUri);
        Assert.Equal($"Bearer {Token}", gh.Sent[0].Authorization);
    }

    private static KitRouter Router(ScriptedReader pending, bool git = true, string? password = null)
    {
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        return new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            password,
            git: new GitStore(enabled: git),
            head: "kit/hosted",
            pending: pending);
    }

    private static JsonElement Body(IKitResponse r) => JsonDocument.Parse(Serialiser.Serialise(r.Body!)).RootElement;

    /// <summary>Answers from a script and records what it was asked, so a test can assert GitHub was NOT asked.</summary>
    private sealed class ScriptedReader(params IPendingEdits[] answers) : IPendingEditsReader
    {
        public List<(string Head, string Base)> Calls { get; } = [];

        public Task<IPendingEdits> PendingAsync(string head, string baseBranch, CancellationToken cancellationToken = default)
        {
            Calls.Add((head, baseBranch));
            return Task.FromResult(answers[Calls.Count - 1]);
        }
    }
}
