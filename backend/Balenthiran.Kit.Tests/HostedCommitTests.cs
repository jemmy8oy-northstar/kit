using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.WebApi;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c>kit-hosted.beh</c> BEH-COMMIT-2. The golden in <c>routes/git.json</c> records the author of
/// a write-back, but only from a clone with no identity of its own; it cannot say the commit
/// stays Kit's when the pod's git config names somebody else, which is the case that decides
/// whether a phone edit is attributed to Kit or to whoever configured the pod.
/// </summary>
public class HostedCommitTests
{
    /// <summary>
    /// His kit#88 override: "let the commit belong to kit". The clone carries a different
    /// <c>user.name</c>/<c>user.email</c> in its own config, as a real pod's might; the commit
    /// the REMOTE receives must still be Kit's, as author and as committer.
    /// </summary>
    [Fact]
    public void The_commit_is_attributed_to_Kit_even_when_the_clone_is_configured_as_someone_else()
    {
        var root = Directory.CreateTempSubdirectory("kit-hosted-").FullName;
        try
        {
            var bare = Path.Combine(root, "remote.git");
            var clone = Path.Combine(root, "clone");
            Run(root, "init", "-q", "--bare", "-b", "main", bare);
            Run(root, "clone", "-q", bare, clone);
            Run(clone, "config", "user.name", "Someone Else");
            Run(clone, "config", "user.email", "someone@example.com");
            File.WriteAllText(Path.Combine(clone, "demo.beh"), "one\n");
            Run(clone, "add", "demo.beh");
            Run(clone, "commit", "-q", "-m", "seed");
            Run(clone, "push", "-q", "origin", "HEAD:main");

            File.WriteAllText(Path.Combine(clone, "demo.beh"), "two\n");
            var r = new GitStore(enabled: true).WriteBack(Path.Combine(clone, "demo.beh"), "add BEH-1", "demo");

            Assert.True(r.Pushed, r.Reason);
            Assert.Equal("kit <kit@users.noreply.github.com>", Run(bare, "log", "-1", "--format=%an <%ae>", "main").Trim());
            Assert.Equal("kit <kit@users.noreply.github.com>", Run(bare, "log", "-1", "--format=%cn <%ce>", "main").Trim());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// BEH-COMMIT-1, "never a commit on dev". The clone sits on <c>dev</c>, as a pod's would if the
    /// entrypoint had not checked out <c>kit/hosted</c>; the write is refused before it is committed,
    /// so neither the clone nor the remote moves. The same edit with an edits branch named goes there.
    /// </summary>
    [Fact]
    public void An_edit_is_never_committed_onto_the_branch_Commit_proposes_into()
    {
        WithCloneOnDev((bare, clone) =>
        {
            var before = Run(bare, "rev-parse", "dev").Trim();
            File.WriteAllText(Path.Combine(clone, "demo.beh"), "two\n");

            var refused = new GitStore(enabled: true, baseBranch: "dev").WriteBack(Path.Combine(clone, "demo.beh"), "add BEH-1", "demo");

            Assert.False(refused.Committed);
            Assert.Equal("dev", refused.Branch);
            Assert.Contains("never committed onto it", refused.Reason);
            Assert.Equal(before, Run(bare, "rev-parse", "dev").Trim());
            Assert.Equal(before, Run(clone, "rev-parse", "HEAD").Trim());

            var r = new GitStore(enabled: true, branch: "kit/hosted", baseBranch: "dev").WriteBack(Path.Combine(clone, "demo.beh"), "add BEH-1", "demo");

            Assert.True(r.Pushed, r.Reason);
            Assert.Equal(before, Run(bare, "rev-parse", "dev").Trim());
            Assert.Equal("two\n", Run(bare, "show", "kit/hosted:demo.beh"));
        });
    }

    /// <summary>
    /// The registration passes <c>KIT_GIT_BASE</c> through: the test above builds its store by hand,
    /// so it cannot see the server's own store left without the guard. No <c>KIT_GIT_BRANCH</c>, so
    /// the store writes to whatever is checked out — <c>dev</c>, the default base.
    /// </summary>
    [Fact]
    public async Task The_registered_server_refuses_an_edit_onto_its_base()
    {
        var env = new Dictionary<string, string?> { ["KIT_GIT"] = "1" };
        var settings = KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root);
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], settings);
        var git = app.Services.GetRequiredService<IGitStore>();

        WithCloneOnDev((bare, clone) =>
        {
            var before = Run(bare, "rev-parse", "dev").Trim();
            File.WriteAllText(Path.Combine(clone, "demo.beh"), "two\n");

            var r = git.WriteBack(Path.Combine(clone, "demo.beh"), "add BEH-1", "demo");

            Assert.False(r.Committed, r.Reason);
            Assert.Equal(before, Run(bare, "rev-parse", "dev").Trim());
        });
    }

    private static void WithCloneOnDev(Action<string, string> body)
    {
        var root = Directory.CreateTempSubdirectory("kit-hosted-").FullName;
        try
        {
            var bare = Path.Combine(root, "remote.git");
            var clone = Path.Combine(root, "clone");
            Run(root, "init", "-q", "--bare", "-b", "dev", bare);
            Run(root, "clone", "-q", bare, clone);
            File.WriteAllText(Path.Combine(clone, "demo.beh"), "one\n");
            Run(clone, "add", "demo.beh");
            Run(clone, "-c", "user.name=seed", "-c", "user.email=seed@example.com", "commit", "-q", "-m", "seed");
            Run(clone, "push", "-q", "origin", "HEAD:dev");
            body(bare, clone);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Run(string cwd, params string[] args)
    {
        var r = GitStore.Git(args, cwd);
        Assert.True(r.Ok, r.Failure);
        return r.Stdout;
    }
}
