using Balenthiran.Kit.Database;
using Balenthiran.Kit.WebApi;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// What <c>routes/git.json</c> cannot reach: write-back switched OFF (the golden only records
/// it on), a git call that fails at each layer, the message format, and the
/// <c>KIT_GIT</c> switch — <c>kit.test.js</c>'s <c>git-store:</c> tests, ported.
/// </summary>
public class GitStoreTests
{
    [Fact]
    public void Off_unless_asked_for_and_the_tree_really_is_untouched()
    {
        var root = Directory.CreateTempSubdirectory("kit-git-").FullName;
        try
        {
            GitStore.Git(["init", "-q", "-b", "main", root], root);
            var file = Path.Combine(root, "demo.beh");
            File.WriteAllText(file, "behaviour BEH-1 \"a thing\"\n");

            var r = new GitStore(enabled: false).WriteBack(file, "add BEH-1", "demo");

            Assert.False(r.Committed);
            Assert.False(r.Pushed);
            Assert.Equal("git write-back is off; the edit is in the working tree only", r.Reason);
            Assert.Contains("demo.beh", GitStore.Git(["status", "--short"], root).Stdout);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_failed_git_call_names_the_layer_that_failed()
    {
        var root = Directory.CreateTempSubdirectory("kit-git-").FullName;
        try
        {
            GitStore.Git(["init", "-q", root], root);

            var exited = GitStore.Git(["rev-parse", "--verify", "refs/heads/does-not-exist"], root);
            Assert.False(exited.Ok);
            Assert.StartsWith("git rev-parse exited 128: ", exited.Failure);

            // A directory that is not there must not read as "git refused the command".
            var gone = GitStore.Git(["rev-parse", "HEAD"], Path.Combine(root, "no-such-dir"));
            Assert.False(gone.Ok);
            Assert.Equal("git could not be run (ENOENT)", gone.Failure);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void The_commit_message_describes_the_edit_and_names_the_app()
    {
        Assert.Equal("kit: add BEH-7 (snip-it)", GitStore.Message("add BEH-7", "snip-it"));
        Assert.Equal("kit: bind page:Home", GitStore.Message("bind page:Home", null));
        Assert.Equal("kit: update", GitStore.Message(null, null));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    public void KIT_GIT_is_on_or_off(string? value, bool on) => Assert.Equal(on, KitSettings.GitSwitch(value));

    [Theory]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData(" 1")]
    public void Any_other_KIT_GIT_refuses_to_start_rather_than_silently_meaning_off(string value)
    {
        var e = Assert.Throws<InvalidOperationException>(() => KitSettings.GitSwitch(value));
        Assert.Contains($"\"{value}\"", e.Message);
    }

    [Fact]
    public void The_environment_reaches_the_settings()
    {
        var env = new Dictionary<string, string?> { ["KIT_GIT"] = "1", ["KIT_GIT_REMOTE"] = "upstream", ["KIT_GIT_BRANCH"] = "kit-edits" };
        var s = KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root);
        Assert.True(s.Git);
        Assert.Equal("upstream", s.GitRemote);
        Assert.Equal("kit-edits", s.GitBranch);

        var off = KitSettings.FromEnvironment(_ => null, RepoLayout.Root);
        Assert.False(off.Git);
        Assert.Null(off.GitRemote);
        Assert.Null(off.GitBranch);
    }
}
