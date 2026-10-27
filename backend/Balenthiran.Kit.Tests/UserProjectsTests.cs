using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
/// kit-ui.beh <c>BEH-USERS-2</c>: Kit lists a user's projects from their own GitHub, with no spec
/// held centrally. A project is a repo the App is installed on that carries a corpus (a
/// <c>.beh</c> file on its default branch), so Kit keeps no list of its own.
/// </summary>
public class UserProjectsTests
{
    private const string Operator = "operator-pw";

    [Fact]
    public async Task A_project_is_a_repo_with_a_beh_file_on_its_default_branch()
    {
        var gh = new Repos()
            .Repo("ann-gh/snip-it", "main", "behaviours/snip-it.beh", "README.md", "src/a.beh.md", "behaviours/extra.beh")
            .Repo("ann-gh/dotfiles", "trunk", "README.md", ".bashrc")
            .Repo("ann-gh/empty", "main", empty: true)
            .Repo("ann-gh/dir-named-beh", "main", ("x.beh", "tree"));

        var found = await Reader(gh).ProjectsAsync(42);

        var one = Assert.Single(found);
        Assert.Equal(("snip-it", "ann-gh/snip-it", "https://github.example/ann-gh/snip-it"), (one.Name, one.FullName, one.Url));
        Assert.Equal(["behaviours/extra.beh", "behaviours/snip-it.beh"], one.Corpus);

        // The tree is read at the default branch the list named, recursively.
        Assert.Contains(gh.Sent, s => s.Uri.AbsolutePath == "/repos/ann-gh/dotfiles/git/trees/trunk" && s.Uri.Query == "?recursive=1");
        Assert.All(gh.Sent, s => Assert.Equal("Bearer ghs_user", s.Authorization));
    }

    [Fact]
    public async Task Every_page_of_the_installation_is_read()
    {
        var gh = new Repos();
        for (var i = 0; i < 100; i++)
        {
            gh.Repo($"ann-gh/plain-{i:000}", "main", "README.md");
        }

        gh.Repo("ann-gh/last", "main", "last.beh");

        var found = await Reader(gh).ProjectsAsync(42);

        Assert.Equal(["ann-gh/last"], found.Select(p => p.FullName));
        Assert.Equal(["page=1", "page=2"], gh.Sent.Where(s => s.Uri.AbsolutePath == "/installation/repositories").Select(s => s.Uri.Query.Split('&')[^1]));
    }

    [Fact]
    public async Task A_token_is_minted_for_the_users_own_installation()
    {
        var key = RSA.Create(2048).ExportRSAPrivateKeyPem();
        var gh = new Repos().Repo("ann-gh/snip-it", "main", "a.beh");
        gh.MintedFor = "ghs_user";

        await using var sp = Services(gh, key, new AccountsTests.MemoryUsers());
        var found = await sp.GetRequiredService<IUserProjects>().ProjectsAsync(42);

        Assert.Single(found);
        var mint = gh.Sent[0];
        Assert.Equal(("POST", "/app/installations/42/access_tokens"), (mint.Method.Method, mint.Uri.AbsolutePath));
        Assert.Equal(3, mint.Authorization!["Bearer ".Length..].Split('.').Length);
        Assert.All(gh.Sent.Skip(1), s => Assert.Equal("Bearer ghs_user", s.Authorization));
    }

    [Fact]
    public async Task GitHub_failing_is_a_reason_never_an_empty_list()
    {
        var down = new Repos { ListStatus = HttpStatusCode.ServiceUnavailable };
        var e = await Assert.ThrowsAsync<GitHubReadException>(() => Reader(down).ProjectsAsync(42));
        Assert.Equal("GitHub answered 503 when listing your repositories", e.Message);

        var oneTree = new Repos().Repo("ann-gh/a", "main", "a.beh");
        oneTree.TreeStatus = HttpStatusCode.Forbidden;
        e = await Assert.ThrowsAsync<GitHubReadException>(() => Reader(oneTree).ProjectsAsync(42));
        Assert.Equal("GitHub answered 403 when reading ann-gh/a", e.Message);

        var refused = new UserProjects(new HttpClient(new Repos()), _ => new Refusing(), new Uri("https://api.github.example/"));
        e = await Assert.ThrowsAsync<GitHubReadException>(() => refused.ProjectsAsync(42));
        Assert.Equal("GitHub answered 401 when minting an installation token", e.Message);

        var none = new UserProjects(new HttpClient(new Repos()), _ => new StaticGitHubTokenSource(null), new Uri("https://api.github.example/"));
        await Assert.ThrowsAsync<GitHubReadException>(() => none.ProjectsAsync(42));
    }

    [Fact]
    public void A_user_with_a_connected_GitHub_sees_those_repos_and_the_operator_is_unaffected()
    {
        var users = Linked();
        var router = Router(users, new Canned([Project("snip-it", "behaviours/snip-it.beh")]));

        var ann = SignIn(router, "ann");
        var hers = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", ann).Body);
        Assert.Empty(hers.Projects);
        Assert.Null(hers.GitHubError);
        var row = Assert.Single(hers.GitHub!);
        Assert.Equal(("snip-it", "ann-gh/snip-it", "https://github.example/ann-gh/snip-it", "behaviours/snip-it.beh"), (row.Name, row.FullName, row.Url, Assert.Single(row.Corpus)));

        // The wire shape: the operator's list carries neither new field.
        var wire = JsonSerializer.Serialize(router.Route("GET", "/api/projects", ann).Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal("""{"projects":[],"gitHub":[{"name":"snip-it","fullName":"ann-gh/snip-it","url":"https://github.example/ann-gh/snip-it","corpus":["behaviours/snip-it.beh"]}]}""", wire);

        var him = SignIn(router, null, Operator);
        var his = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", him).Body);
        Assert.NotEmpty(his.Projects);
        Assert.Null(his.GitHub);
        Assert.Null(his.GitHubError);
    }

    // kit-ui.beh BEH-SETTINGS-3: his personal GitHub's repos sit beside the board's, never instead of them.
    [Fact]
    public void The_operator_with_a_connected_GitHub_keeps_the_boards_projects_and_gains_his_repos()
    {
        var users = Seeded();
        users.Add(ISessionStore.Operator, "!operator");
        Assert.True(users.SetGitHub(ISessionStore.Operator, new GitHubLink { Installation = 42, Account = "james-gh" }));
        var canned = new Canned([Project("tracks", "tracks.beh")]);
        var router = Router(users, canned);

        var him = SignIn(router, null, Operator);
        var his = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", him).Body);
        Assert.NotEmpty(his.Projects);
        Assert.Equal(["ann-gh/tracks"], his.GitHub!.Select(p => p.FullName));
        Assert.Equal(1, canned.Asked);
    }

    [Fact]
    public void No_link_or_no_service_leaves_the_list_empty_as_before()
    {
        var canned = new Canned([Project("snip-it", "a.beh")]);

        var unlinked = Router(Seeded(), canned);
        var body = Assert.IsType<ProjectList>(unlinked.Route("GET", "/api/projects", SignIn(unlinked, "ann")).Body);
        Assert.Empty(body.Projects);
        Assert.Null(body.GitHub);
        Assert.Equal(0, canned.Asked);

        var unserved = Router(Linked(), userProjects: null);
        body = Assert.IsType<ProjectList>(unserved.Route("GET", "/api/projects", SignIn(unserved, "ann")).Body);
        Assert.Empty(body.Projects);
        Assert.Null(body.GitHub);
        Assert.Null(body.GitHubError);
    }

    [Fact]
    public void A_GitHub_failure_is_said_in_the_list_and_is_not_an_empty_one()
    {
        var router = Router(Linked(), new Canned([]) { Fails = "GitHub answered 503 when listing your repositories" });

        var r = router.Route("GET", "/api/projects", SignIn(router, "ann"));

        Assert.Equal(200, r.Status);
        var body = Assert.IsType<ProjectList>(r.Body);
        Assert.Null(body.GitHub);
        Assert.Equal("GitHub answered 503 when listing your repositories", body.GitHubError);
    }

    [Fact]
    public async Task The_server_wires_it_only_when_Kit_holds_the_App_credential()
    {
        var gh = new Repos().Repo("ann-gh/snip-it", "main", "a.beh");
        gh.MintedFor = "ghs_user";
        var users = Linked();
        await using var sp = Services(gh, RSA.Create(2048).ExportRSAPrivateKeyPem(), users);
        var router = sp.GetRequiredService<IKitRouter>();
        var ann = SignIn(router, "ann");

        var body = Assert.IsType<ProjectList>(router.Route("GET", "/api/projects", ann).Body);
        Assert.Equal(["ann-gh/snip-it"], body.GitHub!.Select(p => p.FullName));

        var env = new Dictionary<string, string> { ["KIT_GIT_TOKEN"] = "static" };
        var bare = new ServiceCollection();
        bare.AddKitServices(KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root));
        await using var plain = bare.BuildServiceProvider();
        Assert.Null(plain.GetService<IUserProjects>());
    }

    private static UserProjects Reader(Repos gh) => new(
        new HttpClient(gh), _ => new StaticGitHubTokenSource("ghs_user"), new Uri("https://api.github.example/"));

    private static UserProject Project(string name, params string[] corpus) =>
        new() { Name = name, FullName = $"ann-gh/{name}", Url = $"https://github.example/ann-gh/{name}", Corpus = [.. corpus] };

    private static AccountsTests.MemoryUsers Seeded()
    {
        var users = new AccountsTests.MemoryUsers();
        users.Add("ann", PasswordHash.Hash("ann-first-password", iterations: 1000));
        return users;
    }

    private static AccountsTests.MemoryUsers Linked()
    {
        var users = Seeded();
        users.SetGitHub("ann", new GitHubLink { Installation = 42, Account = "ann-gh" });
        return users;
    }

    private static ServiceProvider Services(Repos gh, string key, IUserStore users)
    {
        var env = new Dictionary<string, string>
        {
            ["KIT_PASSWORD"] = Operator,
            ["KIT_GITHUB_APP_ID"] = "123",
            ["KIT_GITHUB_INSTALLATION_ID"] = "456",
            ["KIT_GITHUB_PRIVATE_KEY"] = key,
        };
        var services = new ServiceCollection();
        services.AddSingleton<HttpMessageHandler>(gh);
        services.AddSingleton(users);
        services.AddKitServices(KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root));
        return services.BuildServiceProvider();
    }

    private static string SignIn(IKitRouter router, string? name, string password = "ann-first-password")
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

    /// <summary>What the service would return, or the reason it would throw.</summary>
    private sealed class Canned(IReadOnlyList<UserProject> projects) : IUserProjects
    {
        public int Asked { get; private set; }

        public string? Fails { get; init; }

        public Task<IReadOnlyList<IUserProject>> ProjectsAsync(long installation, CancellationToken cancellationToken = default)
        {
            Asked++;
            Assert.Equal(42, installation);
            return Fails is { } why ? throw new GitHubReadException(why) : Task.FromResult<IReadOnlyList<IUserProject>>([.. projects]);
        }
    }

    private sealed class Refusing : IGitHubTokenSource
    {
        public Task<string?> TokenAsync(CancellationToken cancellationToken = default) =>
            throw new GitHubTokenException("GitHub answered 401 when minting an installation token");
    }

    /// <summary>
    /// GitHub as the reader sees it: an installation's repositories, 100 to a page, and each
    /// repo's tree. Routes by path, because the trees are read concurrently and arrive in any order.
    /// </summary>
    private sealed class Repos : HttpMessageHandler
    {
        private readonly List<(string Full, string Branch, (string Path, string Type)[] Tree, bool Empty)> repos = [];
        private readonly object gate = new();

        public List<(HttpMethod Method, Uri Uri, string? Authorization)> Sent { get; } = [];

        public HttpStatusCode ListStatus { get; init; } = HttpStatusCode.OK;

        public HttpStatusCode TreeStatus { get; set; } = HttpStatusCode.OK;

        /// <summary>When set, the first POST (a mint) answers this token.</summary>
        public string? MintedFor { get; set; }

        public Repos Repo(string full, string branch, params string[] files)
        {
            repos.Add((full, branch, files.Select(f => (f, "blob")).ToArray(), false));
            return this;
        }

        public Repos Repo(string full, string branch, params (string Path, string Type)[] entries)
        {
            repos.Add((full, branch, entries, false));
            return this;
        }

        public Repos Repo(string full, string branch, bool empty)
        {
            repos.Add((full, branch, [], empty));
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                Sent.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.ToString()));
            }

            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && MintedFor is { } token && path.EndsWith("/access_tokens", StringComparison.Ordinal))
            {
                return Task.FromResult(Answer(HttpStatusCode.Created, JsonSerializer.Serialize(new { token, expires_at = DateTimeOffset.UtcNow.AddHours(1).ToString("O") })));
            }

            if (path == "/installation/repositories")
            {
                var page = int.Parse(System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["page"]!, System.Globalization.CultureInfo.InvariantCulture);
                var rows = repos.Skip((page - 1) * 100).Take(100).Select(r => new
                {
                    name = r.Full.Split('/')[1],
                    full_name = r.Full,
                    html_url = $"https://github.example/{r.Full}",
                    default_branch = r.Branch,
                });
                return Task.FromResult(Answer(ListStatus, JsonSerializer.Serialize(new { total_count = repos.Count, repositories = rows })));
            }

            foreach (var r in repos)
            {
                if (path == $"/repos/{r.Full}/git/trees/{r.Branch}")
                {
                    return Task.FromResult(r.Empty
                        ? Answer(HttpStatusCode.Conflict, """{"message":"Git Repository is empty."}""")
                        : Answer(TreeStatus, JsonSerializer.Serialize(new { truncated = false, tree = r.Tree.Select(t => new { path = t.Path, type = t.Type }) })));
                }
            }

            throw new InvalidOperationException($"unscripted request: {request.Method} {request.RequestUri}");
        }

        private static HttpResponseMessage Answer(HttpStatusCode status, string json) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
