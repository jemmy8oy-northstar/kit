using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.WebApi;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit#88, kit#118: the App does writes as well as reads. A deployed Kit said "Git write-back is off"
/// because the pod's only push credential was the static <c>KIT_GIT_TOKEN</c> — the App credential
/// reached reads and Commit's PR but never git. So: every push is handed a token from the same
/// source, and the entrypoint clones with one printed by <c>github-token</c>.
/// </summary>
public class AppTokenWriteBackTests
{
    /// <summary>
    /// A pre-push hook runs in the push's own environment, so it records the <c>KIT_GIT_TOKEN</c>
    /// git's credential helper would have read — over a file remote, which never asks for one.
    /// Two pushes, two mints: the second must not reuse the first, or an hour-old token fails.
    /// </summary>
    [Fact]
    public void Every_push_carries_a_token_from_the_source_not_the_one_the_clone_was_made_with()
    {
        using var repo = new Repo();
        var tokens = new Scripted("ghs_first", "ghs_second");
        var store = new GitStore(enabled: true, tokens: tokens);

        var one = store.WriteBack(repo.Write("one\n"), "add BEH-1", "demo");
        var two = store.WriteBack(repo.Write("two\n"), "add BEH-2", "demo");

        Assert.True(one.Pushed, one.Reason);
        Assert.True(two.Pushed, two.Reason);
        Assert.Equal(["ghs_first", "ghs_second"], repo.TokensSeenByPush());
        Assert.Equal(2, tokens.Asked);
    }

    [Fact]
    public void No_token_configured_pushes_with_the_environment_as_before()
    {
        using var repo = new Repo();

        var r = new GitStore(enabled: true, tokens: new StaticGitHubTokenSource(null)).WriteBack(repo.Write("one\n"), "add BEH-1", "demo");

        Assert.True(r.Pushed, r.Reason);
        Assert.Equal([Environment.GetEnvironmentVariable("KIT_GIT_TOKEN") ?? string.Empty], repo.TokensSeenByPush());
    }

    /// <summary>A token that cannot be minted is a push that did not happen — said, and the commit kept, like a rejected push.</summary>
    [Fact]
    public void A_token_that_cannot_be_minted_keeps_the_commit_and_says_why_it_was_not_pushed()
    {
        using var repo = new Repo();
        var before = repo.RemoteHead();

        var r = new GitStore(enabled: true, tokens: new Scripted()).WriteBack(repo.Write("one\n"), "add BEH-1", "demo");

        Assert.True(r.Committed);
        Assert.False(r.Pushed);
        Assert.Equal("GitHub refused the App's JWT (401)", r.Reason);
        Assert.Equal(before, repo.RemoteHead());
        Assert.Empty(repo.TokensSeenByPush());
    }

    /// <summary>The server's own wiring: a write-back store built from settings pushes with the App's token.</summary>
    [Fact]
    public void The_server_hands_its_git_store_the_same_token_source()
    {
        using var repo = new Repo();
        var gh = new FakeGitHub(MintedToken("ghs_from_the_app"));
        var settings = AppSettings();
        var services = new ServiceCollection();
        services.AddSingleton<HttpMessageHandler>(gh);
        services.AddKitServices(settings with { Git = true });
        using var sp = services.BuildServiceProvider();

        var r = sp.GetRequiredService<IGitStore>().WriteBack(repo.Write("one\n"), "add BEH-1", "demo");

        Assert.True(r.Pushed, r.Reason);
        Assert.Equal(["ghs_from_the_app"], repo.TokensSeenByPush());
    }

    [Fact]
    public async Task Github_token_prints_the_minted_token_and_nothing_else()
    {
        var (stdout, stderr) = (new StringWriter(), new StringWriter());

        var code = await GitHubTokenCommand.RunAsync(AppSettings(), stdout, stderr, new FakeGitHub(MintedToken("ghs_for_the_clone")));

        Assert.Equal(0, code);
        Assert.Equal("ghs_for_the_clone", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public async Task Github_token_with_no_credential_exits_1_with_nothing_on_stdout()
    {
        var (stdout, stderr) = (new StringWriter(), new StringWriter());

        var code = await GitHubTokenCommand.RunAsync(KitSettings.FromEnvironment(_ => null, RepoLayout.Root), stdout, stderr, new FakeGitHub());

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.StartsWith("kit github-token: no credential is configured", stderr.ToString());
    }

    [Fact]
    public async Task Github_token_that_GitHub_refuses_exits_1_with_the_reason_and_nothing_on_stdout()
    {
        var (stdout, stderr) = (new StringWriter(), new StringWriter());

        var code = await GitHubTokenCommand.RunAsync(AppSettings(), stdout, stderr, new FakeGitHub(FakeGitHub.Json(HttpStatusCode.Unauthorized, "{}")));

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.StartsWith("kit github-token: ", stderr.ToString());
        Assert.Contains("401", stderr.ToString());
    }

    private static readonly string Pem = RSA.Create(2048).ExportRSAPrivateKeyPem();

    private static KitSettings AppSettings()
    {
        var env = new Dictionary<string, string>
        {
            ["KIT_GITHUB_APP_ID"] = "123456",
            ["KIT_GITHUB_INSTALLATION_ID"] = "7890",
            ["KIT_GITHUB_PRIVATE_KEY"] = Pem,
        };
        return KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> MintedToken(string token) =>
        FakeGitHub.Json(HttpStatusCode.Created, JsonSerializer.Serialize(new { token, expires_at = DateTimeOffset.UtcNow.AddHours(1).ToString("O") }));

    /// <summary>Hands out the tokens it was given, one per ask; out of tokens, it fails as a refused JWT does.</summary>
    private sealed class Scripted(params string[] tokens) : IGitHubTokenSource
    {
        public int Asked { get; private set; }

        public Task<string?> TokenAsync(CancellationToken cancellationToken = default) =>
            Asked < tokens.Length
                ? Task.FromResult<string?>(tokens[Asked++])
                : throw new GitHubTokenException("GitHub refused the App's JWT (401)");
    }

    /// <summary>A bare remote, a clone of it on <c>main</c>, and a pre-push hook that logs <c>$KIT_GIT_TOKEN</c>.</summary>
    private sealed class Repo : IDisposable
    {
        private readonly string root = Directory.CreateTempSubdirectory("kit-app-push-").FullName;

        public Repo()
        {
            var bare = Path.Combine(root, "remote.git");
            GitStore.Git(["init", "-q", "--bare", "-b", "main", bare], root);
            GitStore.Git(["clone", "-q", bare, Clone], root);
            File.WriteAllText(File_, "seed\n");
            GitStore.Git(["add", "demo.beh"], Clone);
            GitStore.Git(["-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "seed"], Clone);
            GitStore.Git(["push", "-q", "origin", "HEAD:main"], Clone);

            var hook = Path.Combine(Clone, ".git", "hooks", "pre-push");
            File.WriteAllText(hook, $"#!/bin/sh\nprintf '%s\\n' \"$KIT_GIT_TOKEN\" >> '{Log}'\n");
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        private string Clone => Path.Combine(root, "clone");

        private string File_ => Path.Combine(Clone, "demo.beh");

        private string Log => Path.Combine(root, "push-tokens.log");

        public string Write(string text)
        {
            File.WriteAllText(File_, text);
            return File_;
        }

        public string[] TokensSeenByPush() => File.Exists(Log) ? File.ReadAllLines(Log) : [];

        public string RemoteHead() => GitStore.Git(["rev-parse", "main"], Path.Combine(root, "remote.git")).Stdout.Trim();

        public void Dispose() => Directory.Delete(root, recursive: true);
    }
}
