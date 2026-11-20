using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-FLAGS-3</c>: a tester's Projects, Board, Inbox and Settings pages are on, and show
/// only what their own account reaches; every write to a project they do not own, and every operator-only
/// write, is refused; no tester action starts an agent run. Also kit.beh <c>BEH-SCOPE-1</c> and
/// <c>BEH-SCOPE-2</c>, for the part that is built: a tester's list is per user, found by their own
/// installation. Not built, so not tested here: a tester's writes to their own items, their own projects'
/// boards, per-user proposals, and discovery by contributor name.
/// </summary>
public class TesterFeatureAccessTests
{
    private const string Operator = "operator-pw";
    private const string AnnPassword = "ann-first-password";
    private const string BobPassword = "bob-first-password";

    [Fact]
    public void BEH_FLAGS_3_a_tester_list_holds_their_own_repos_and_none_of_the_operators()
    {
        var users = Linked();
        var ann = new Installed(new Dictionary<long, IReadOnlyList<UserProject>>
        {
            [42] = [Repo("ann-gh", "snip-it")],
        });
        var router = Router(users, ann);

        var cookie = SignIn(router, "ann");
        var list = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", cookie).Body);

        Assert.Empty(list.Projects);
        Assert.Equal(["ann-gh/snip-it"], list.GitHub!.Select(r => r.FullName));

        // The operator's list is what it was: his projects, and none of Ann's repos.
        var him = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", SignIn(router, null, Operator)).Body);
        Assert.NotEmpty(him.Projects);
        Assert.Null(him.GitHub);
    }

    [Fact]
    public void BEH_FLAGS_3_a_tester_with_no_link_sees_no_projects_at_all()
    {
        var router = Router(Seeded(), new Installed(new Dictionary<long, IReadOnlyList<UserProject>>()));

        var list = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", SignIn(router, "ann")).Body);

        Assert.Empty(list.Projects);
        Assert.Null(list.GitHub);
    }

    [Fact]
    public void BEH_FLAGS_3_a_tester_is_refused_an_operator_project_by_url_and_it_is_never_in_their_list()
    {
        // BEH-USERS-8 (kit#795, 2026-11-14, "Viewable kit specs should be determined by the projects in your
        // github"): another user opening the operator's project by its URL gets 403 not-yours, whatever repo it
        // is in, so a public project is no longer read by URL. It is not theirs, so their list does not hold it.
        var router = Router(Linked(), new Installed(new Dictionary<long, IReadOnlyList<UserProject>>()));
        var cookie = SignIn(router, "ann");
        var name = OperatorProject();

        var read = router.Route("GET", $"/api/projects/{name}", cookie);

        Assert.Equal(403, read.Status);
        var list = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", cookie).Body);
        Assert.Empty(list.Projects);
    }

    [Fact]
    public void BEH_FLAGS_3_a_tester_write_to_a_project_they_do_not_own_is_refused()
    {
        // Approve, deny, answer, pause and lower priority are the tester's own actions (the assumption on the
        // card), but only on their own items, and this project is the operator's. Every project write is refused.
        var router = Router(Linked(), new Installed(new Dictionary<long, IReadOnlyList<UserProject>>()));
        var cookie = SignIn(router, "ann");
        var app = OperatorProject();

        string[] writes =
        [
            $"/api/projects/{app}/behaviours/BEH-X/review/approved",
            $"/api/projects/{app}/behaviours/BEH-X/review/denied",
            $"/api/projects/{app}/behaviours/BEH-X/paused/true",
            $"/api/projects/{app}/behaviours/BEH-X/priority/1",
            $"/api/projects/{app}/notes/reply",
            $"/api/projects/{app}/commit",
        ];

        foreach (var path in writes)
        {
            AssertRefusedAsNotYours(router.Route("POST", path, cookie), path);
        }
    }

    [Fact]
    public void BEH_FLAGS_3_each_operator_only_write_is_refused_for_a_tester()
    {
        // The operator's sections on Settings: promotion, depth, conflicts, idle order and feature switches.
        var router = Router(Linked(), new Installed(new Dictionary<long, IReadOnlyList<UserProject>>()));
        var cookie = SignIn(router, "ann");
        var app = OperatorProject();

        string[] writes =
        [
            $"/api/projects/{app}/promote",
            $"/api/projects/{app}/depth",
            $"/api/projects/{app}/conflicts",
            "/api/settings/idle",
            "/api/settings/features",
        ];

        foreach (var path in writes)
        {
            AssertRefusedAsNotYours(router.Route("POST", path, cookie), path);
        }
    }

    [Fact]
    public void BEH_FLAGS_3_no_tester_action_starts_or_lists_an_agent_run()
    {
        // BEH-USERS-9 costs money per run, so a tester's cards never start one (the card's assumption), and
        // the operator's runs are not listed to them.
        var router = Router(Linked(), new Installed(new Dictionary<long, IReadOnlyList<UserProject>>()));
        var cookie = SignIn(router, "ann");
        var app = OperatorProject();

        AssertRefusedAsNotYours(router.Route("POST", $"/api/projects/{app}/runs", cookie, body: Json(new { model = "any" })), "runs");

        var listed = router.Route("GET", "/api/runs", cookie);
        Assert.Equal(403, listed.Status);
        Assert.Equal("not-yours", Json(listed.Body!).GetProperty("error").GetString());
    }

    [Fact]
    public void BEH_FLAGS_3_a_tester_settings_name_their_account_and_no_operator_section()
    {
        var router = Router(Linked(), new Installed(new Dictionary<long, IReadOnlyList<UserProject>>()));
        var cookie = SignIn(router, "ann");

        var session = Json(router.Route("GET", "/api/session", cookie).Body!);
        Assert.Equal("ann", session.GetProperty("user").GetString());
        Assert.Equal("ann-gh", session.GetProperty("gitHub").GetString());
        Assert.False(session.TryGetProperty("operator", out _));

        // The features the page reads for everyone: the group and what is off, never the operator's list.
        var features = Json(router.Route("GET", "/api/settings/features", cookie).Body!);
        Assert.Equal("testers", features.GetProperty("group").GetString());
        Assert.True(features.TryGetProperty("off", out _));
        Assert.False(features.TryGetProperty("features", out _));
    }

    [Fact]
    public void BEH_FLAGS_3_a_page_switched_off_for_testers_is_listed_off_and_the_pages_on_are_not()
    {
        // BEH-FLAGS-2 shows "Not available" for a page listed here; the pages not listed keep working.
        var (router, _) = SwitchRig("# kit:switch FEAT-A testers on\n");
        var cookie = SignIn(router, "ann");

        var features = Assert.IsType<FeatureSwitches>(router.Route("GET", "/api/settings/features", cookie).Body);

        Assert.Equal(["FEAT-B"], features.Off);
    }

    [Fact]
    public void BEH_FLAGS_3_a_tester_inbox_feed_holds_no_operator_items()
    {
        // The Inbox reads the same project list as the Board (BEH-INBOX-1), and the agent's messages.
        var router = Router(Linked(), new Installed(new Dictionary<long, IReadOnlyList<UserProject>>()));
        var cookie = SignIn(router, "ann");

        var feed = Assert.IsType<NotificationFeed>(router.Route("GET", "/api/notifications", cookie).Body);

        Assert.Empty(feed.Entries);
    }

    [Fact]
    public void BEH_SCOPE_1_a_second_tester_never_lists_the_first_testers_projects()
    {
        // Each tester's list is found by their own installation, so one tester's repos are never another's.
        var users = Linked();
        users.Add("bob", PasswordHash.Hash(BobPassword, iterations: 1000));
        users.SetGitHub("bob", new GitHubLink { Installation = 43, Account = "bob-gh" });
        var router = Router(users, new Installed(new Dictionary<long, IReadOnlyList<UserProject>>
        {
            [42] = [Repo("ann-gh", "snip-it")],
            [43] = [Repo("bob-gh", "trip-plan")],
        }));

        var bob = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", SignIn(router, "bob", BobPassword)).Body);
        var ann = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", SignIn(router, "ann")).Body);

        Assert.Equal(["bob-gh/trip-plan"], bob.GitHub!.Select(r => r.FullName));
        Assert.Equal(["ann-gh/snip-it"], ann.GitHub!.Select(r => r.FullName));
    }

    [Fact]
    public void BEH_SCOPE_2_a_repo_with_a_corpus_is_listed_for_the_user_it_is_connected_to_and_no_other()
    {
        // Built as the installation the repo is found through, not yet by a contributor name in the repo: that
        // half of the card is not built. This pins the "and for no other user" half.
        var users = Linked();
        users.Add("bob", PasswordHash.Hash(BobPassword, iterations: 1000));
        users.SetGitHub("bob", new GitHubLink { Installation = 43, Account = "bob-gh" });
        var router = Router(users, new Installed(new Dictionary<long, IReadOnlyList<UserProject>>
        {
            [42] = [Repo("ann-gh", "holiday-planner")],
        }));

        var ann = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", SignIn(router, "ann")).Body);
        var bob = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", SignIn(router, "bob", BobPassword)).Body);

        Assert.Equal(["ann-gh/holiday-planner"], ann.GitHub!.Select(r => r.FullName));
        Assert.Empty(bob.GitHub!);
    }

    // ---- rigs --------------------------------------------------------------------------------

    private static UserProject Repo(string owner, string name) => new()
    {
        Name = name,
        FullName = $"{owner}/{name}",
        Url = $"https://github.example/{owner}/{name}",
        Corpus = [$"behaviours/{name}.beh"],
    };

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static void AssertRefusedAsNotYours(IKitResponse r, string path)
    {
        Assert.True(r.Status == 403, $"{path} answered {r.Status}, not 403");
        Assert.Equal("not-yours", Json(r.Body!).GetProperty("error").GetString());
    }

    /// <summary>A project that is the operator's: one of the corpora Kit serves, none of them the tester's.</summary>
    private static string OperatorProject() =>
        new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot).Corpora().First();

    private static AccountsTests.MemoryUsers Seeded()
    {
        var users = new AccountsTests.MemoryUsers();
        users.Add("ann", PasswordHash.Hash(AnnPassword, iterations: 1000));
        return users;
    }

    private static AccountsTests.MemoryUsers Linked()
    {
        var users = Seeded();
        users.SetGitHub("ann", new GitHubLink { Installation = 42, Account = "ann-gh" });
        return users;
    }

    private static string SignIn(KitRouter router, string? name, string password = AnnPassword)
    {
        var body = JsonSerializer.SerializeToElement(name is null ? new { password } : (object)new { username = name, password });
        var r = router.Route("POST", "/api/session", body: body);
        Assert.Equal(200, r.Status);
        return r.SetCookie!.Split(';')[0];
    }

    private static KitRouter Router(IUserStore users, IUserProjects? userProjects)
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
            users: users,
            userProjects: userProjects);
    }

    /// <summary>A Kit with one switch line for testers, over a two-feature corpus (as FlagsTests does).</summary>
    private static (KitRouter Router, string Dir) SwitchRig(string switchLine)
    {
        var text = switchLine
            + "# kit:project kit\n# kit:repo o/r@dev\n\nfeature FEAT-A \"Alpha\"\n  stage ux\n\nfeature FEAT-B \"Beta\"\n  stage ux\n\nbehaviour BEH-1 \"a thing\"\n  in FEAT-A\n  when opens page:Home\n";
        var dir = Directory.CreateTempSubdirectory("kit-tester-").FullName;
        File.WriteAllText(Path.Combine(dir, "kit.beh"), text);

        var corpora = new CorpusDirectory(dir, RepoLayout.Root);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        var users = Seeded();
        users.SetGitHub("ann", new GitHubLink { Installation = 42, Account = "ann-gh" });
        var router = new KitRouter(
            corpora, viewer, new UiBundle(Path.Combine(dir, "no-bundle")), Operator,
            new OriginPolicy(new UrlParser(), null), new SessionStore(), new SignInThrottle(), users: users);
        return (router, dir);
    }

    /// <summary>Each installation's repos: a tester sees only the ones their own installation reaches.</summary>
    private sealed class Installed(IReadOnlyDictionary<long, IReadOnlyList<UserProject>> repos) : IUserProjects
    {
        public Task<IReadOnlyList<IUserProject>> ProjectsAsync(long installation, CancellationToken cancellationToken = default)
        {
            var found = new List<IUserProject>();
            if (repos.TryGetValue(installation, out var mine))
            {
                found.AddRange(mine);
            }

            return Task.FromResult<IReadOnlyList<IUserProject>>(found);
        }

        public Task<IReadOnlyDictionary<string, string>> FilesAsync(long installation, string fullName, Func<string, bool> wanted, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }
}
