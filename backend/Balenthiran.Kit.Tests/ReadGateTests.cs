using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-hosted.beh <c>BEH-GATE-H1</c> (kit#795): a project whose repo is public is read by anyone, and
/// one whose repo is not is read signed in. Every case runs locked as well as open, because an open
/// read on a server with no password proves nothing about the lock.
/// </summary>
public class ReadGateTests
{
    private static readonly string[] PerProject = ["", "/notes", "/schedule", "/pending", "/history"];

    [Fact]
    public void Locked_a_private_project_needs_a_session_and_a_public_one_does_not()
    {
        var router = Router(password: "pw", new PublicApps("snip-it"));

        // The lock is genuinely on: without this the reads below would pass on an unlocked server.
        Assert.Equal(401, router.Route("POST", "/api/commit").Status);

        foreach (var tail in PerProject)
        {
            var refused = router.Route("GET", $"/api/projects/kit{tail}");
            Assert.Equal(401, refused.Status);
            Assert.Equal("not-signed-in", Assert.IsType<ApiError>(refused.Body).Error);
            Assert.NotEqual(401, router.Route("GET", $"/api/projects/snip-it{tail}").Status);
        }

        // Signed in, both are served: the gate is on the caller, not the project.
        var him = Cookie(router);
        Assert.Equal(200, router.Route("GET", "/api/projects/kit", him).Status);
        Assert.Equal(200, router.Route("GET", "/api/projects/snip-it", him).Status);
    }

    [Fact]
    public void A_public_projects_view_names_no_private_project_to_a_stranger()
    {
        // A frozen app whose view, signed in, names others that share its nouns: else the check is empty.
        var probe = Router(password: "pw", visibility: null);
        var him = Cookie(probe);
        var app = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot).Corpora()
            .First(a => SharedWith(probe.Route("GET", $"/api/projects/{a}", him)).Count > 0);

        var router = Router(password: "pw", new PublicApps(app));
        Assert.NotEmpty(SharedWith(router.Route("GET", $"/api/projects/{app}", Cookie(router))));
        Assert.Empty(SharedWith(router.Route("GET", $"/api/projects/{app}")));
    }

    [Fact]
    public async Task A_refresh_that_breaks_locks_every_project_and_does_not_throw()
    {
        using var dir = new Corpora(("open", "o/open@dev"));
        var reader = new ScriptedRepos(new() { ["o/open"] = true });
        var store = new ProjectVisibility(dir.Directory, reader);
        await store.RefreshAsync();
        Assert.True(store.IsPublic("open"));

        // The one exception the per-repo catch lets through, so this reaches the outer one.
        reader.Unexpected = new OperationCanceledException("cancelled mid-read");
        await store.RefreshAsync();
        Assert.False(store.IsPublic("open"));
    }

    private static List<string> SharedWith(IKitResponse r) =>
        Assert.IsType<ProjectView>(r.Body).Requires.Nouns.SelectMany(n => n.SharedWith).Distinct().ToList();

    [Fact]
    public void A_signed_out_list_holds_only_the_public_projects()
    {
        var router = Router(password: "pw", new PublicApps("snip-it"));

        Assert.Equal(["snip-it"], Apps(router.Route("GET", "/api/projects")));
        Assert.Contains("kit", Apps(router.Route("GET", "/api/projects", Cookie(router))));
    }

    [Fact]
    public void With_no_answer_about_visibility_every_project_is_private()
    {
        // Fail closed: local Kit has no store, and a hosted Kit before its first read knows nothing.
        var router = Router(password: "pw", visibility: null);

        Assert.Equal(401, router.Route("GET", "/api/projects/snip-it").Status);
        Assert.Empty(Apps(router.Route("GET", "/api/projects")));
    }

    [Fact]
    public void A_signed_out_caller_cannot_tell_a_private_project_from_a_missing_one()
    {
        var router = Router(password: "pw", new PublicApps("snip-it"));

        Assert.Equal(401, router.Route("GET", "/api/projects/no-such-app").Status);
        Assert.Equal(404, router.Route("GET", "/api/projects/no-such-app", Cookie(router)).Status);
    }

    [Fact]
    public void The_activity_feed_needs_a_session()
    {
        var router = Router(password: "pw", new PublicApps("snip-it"));

        var refused = router.Route("GET", "/api/activity");
        Assert.Equal(401, refused.Status);
        Assert.Equal("not-signed-in", Assert.IsType<ApiError>(refused.Body).Error);
        Assert.Equal(200, router.Route("GET", "/api/activity", Cookie(router)).Status);
    }

    [Fact]
    public void What_the_agent_reads_signed_out_stays_open()
    {
        var router = Router(password: "pw", visibility: null);

        foreach (var path in new[] { "/api/health", "/api/session", "/api/settings/idle" })
        {
            Assert.Equal(200, router.Route("GET", path).Status);
        }
    }

    [Fact]
    public void Unlocked_loopback_reads_everything_and_any_other_interface_reads_only_the_public()
    {
        Assert.Equal(200, Router(password: null, visibility: null).Route("GET", "/api/projects/kit").Status);
        Assert.Equal(200, Router(password: null, visibility: null).Route("GET", "/api/activity").Status);

        var routable = Router(password: null, new PublicApps("snip-it"), host: "0.0.0.0");
        Assert.Equal(401, routable.Route("GET", "/api/projects/kit").Status);
        Assert.Equal(200, routable.Route("GET", "/api/projects/snip-it").Status);
    }

    [Fact]
    public async Task The_store_lists_an_app_only_when_GitHub_said_its_repo_is_public()
    {
        using var dir = new Corpora(("open", "o/open@dev"), ("open-too", "o/open@main"), ("shut", "o/shut@dev"), ("unnamed", null));
        var reader = new ScriptedRepos(new() { ["o/open"] = true, ["o/shut"] = false });
        var store = new ProjectVisibility(dir.Directory, reader);

        Assert.False(store.IsPublic("open")); // before the first read
        await store.RefreshAsync();

        Assert.True(store.IsPublic("open"));
        Assert.True(store.IsPublic("open-too"));
        Assert.False(store.IsPublic("shut"));
        Assert.False(store.IsPublic("unnamed")); // no line: its only copy is in Kit's own repo
        Assert.Equal(["o/open", "o/shut"], reader.Asked.Order(StringComparer.Ordinal)); // once per repo, branch dropped

        // A repo made private is locked on the next read, and so is one that stops answering.
        reader.Answers["o/open"] = false;
        await store.RefreshAsync();
        Assert.False(store.IsPublic("open"));

        reader.Answers.Clear();
        reader.Answers["o/open"] = true;
        await store.RefreshAsync();
        Assert.True(store.IsPublic("open"));
        reader.Answers.Clear(); // now it throws
        await store.RefreshAsync();
        Assert.False(store.IsPublic("open"));
    }

    /// <summary>A throwaway corpora directory: one <c>.beh</c> per app, with the <c>kit:repo</c> line given.</summary>
    private sealed class Corpora : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"kit-visibility-{Guid.NewGuid():N}");

        public Corpora(params (string App, string? Repo)[] apps)
        {
            System.IO.Directory.CreateDirectory(root);
            foreach (var (app, repo) in apps)
            {
                File.WriteAllText(Path.Combine(root, $"{app}.beh"), repo is null ? "# no repo\n" : $"# kit:repo {repo}\n");
            }

            Directory = new CorpusDirectory(root, root);
        }

        public CorpusDirectory Directory { get; }

        public void Dispose() => System.IO.Directory.Delete(root, recursive: true);
    }

    private static List<string> Apps(IKitResponse r) =>
        Assert.IsType<ProjectList>(r.Body).Projects
            .Select(p => System.Text.Json.JsonSerializer.SerializeToElement(p).GetProperty("App").GetString()!).ToList();

    private static string Cookie(KitRouter router)
    {
        var body = System.Text.Json.JsonSerializer.SerializeToElement(new { password = "pw" });
        var signedIn = router.Route("POST", "/api/session", body: body);
        return signedIn.SetCookie!.Split(';')[0];
    }

    private static KitRouter Router(string? password, IProjectVisibility? visibility, string host = "127.0.0.1")
    {
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        return new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            password,
            new OriginPolicy(new UrlParser(), null),
            new SessionStore(),
            new SignInThrottle(),
            host: host,
            visibility: visibility);
    }

    /// <summary>A store that already answered: these apps are public, every other is not.</summary>
    internal sealed class PublicApps(params string[] apps) : IProjectVisibility
    {
        public bool IsPublic(string app) => apps.Contains(app, StringComparer.Ordinal);
    }

    private sealed class ScriptedRepos(Dictionary<string, bool> answers) : IGitHubRepoReader
    {
        public Dictionary<string, bool> Answers { get; } = answers;

        public List<string> Asked { get; } = [];

        public Exception? Unexpected { get; set; }

        public Task<bool> IsPublicAsync(string repo, CancellationToken cancellationToken = default)
        {
            Asked.Add(repo);
            if (Unexpected is not null)
            {
                throw Unexpected;
            }

            return Answers.TryGetValue(repo, out var open)
                ? Task.FromResult(open)
                : throw new Abstractions.Exceptions.GitHubReadException($"{repo}: GitHub answered 404");
        }
    }
}
