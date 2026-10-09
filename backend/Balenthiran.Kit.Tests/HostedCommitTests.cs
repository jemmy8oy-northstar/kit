using Balenthiran.Kit.Database;

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

    private static string Run(string cwd, params string[] args)
    {
        var r = GitStore.Git(args, cwd);
        Assert.True(r.Ok, r.Failure);
        return r.Stdout;
    }
}
