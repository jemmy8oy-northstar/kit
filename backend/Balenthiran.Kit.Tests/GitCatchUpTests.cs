using Balenthiran.Kit.Database;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit#300, BEH-COMMIT-6: "When I merge spec into dev is it coming up on boards?" It was not until
/// a restart. <see cref="GitStore.CatchUp"/> is the entrypoint's merge, run while Kit runs. Each
/// test is a real bare remote and real clones, scored by what the REMOTE's edits branch holds.
/// </summary>
public sealed class GitCatchUpTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("kit-catchup-").FullName;
    private readonly string bare;
    private readonly string pod;
    private readonly string dev;

    public GitCatchUpTests()
    {
        bare = Path.Combine(root, "remote.git");
        pod = Path.Combine(root, "pod");
        dev = Path.Combine(root, "dev");
        Git(root, "init", "-q", "--bare", "-b", "dev", bare);
        Git(root, "clone", "-q", bare, dev);
        Commit(dev, "kit.beh", "behaviour BEH-1 \"one\"\n", "seed");
        Git(dev, "push", "-q", "origin", "HEAD:dev");
        Git(dev, "push", "-q", "origin", "HEAD:kit/hosted");

        // The pod: a clone on the edits branch, as the entrypoint leaves it.
        Git(root, "clone", "-q", "--branch", "kit/hosted", bare, pod);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    private static string Git(string cwd, params string[] args)
    {
        var r = GitStore.Git(args, cwd);
        Assert.True(r.Ok, r.Failure);
        return r.Stdout.Trim();
    }

    private static void Commit(string cwd, string file, string text, string message)
    {
        File.WriteAllText(Path.Combine(cwd, file), text);
        Git(cwd, "add", file);
        Git(cwd, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", message);
    }

    private string Remote(string branch) => Git(root, "--git-dir", bare, "rev-parse", branch);

    private static GitStore Store(bool enabled = true, string? baseBranch = "dev") => new(enabled, baseBranch: baseBranch);

    [Fact]
    public void A_commit_merged_into_dev_reaches_the_edits_branch_on_the_remote()
    {
        Commit(pod, "kit.notes.md", "## a note\n", "kit: leave a note (kit)");
        Git(pod, "push", "-q", "origin", "HEAD:kit/hosted");
        Commit(dev, "kit-ui.beh", "behaviour BEH-2 \"two\"\n", "spec: merged into dev");
        Git(dev, "push", "-q", "origin", "HEAD:dev");

        var r = Store().CatchUp(pod);

        Assert.True(r.Committed, r.Reason);
        Assert.True(r.Pushed, r.Reason);
        Assert.Equal("kit/hosted", r.Branch);
        // Dev's commit is now on the REMOTE edits branch, which is what KIT_PROJECTS reads.
        Assert.True(GitStore.Git(["--git-dir", bare, "merge-base", "--is-ancestor", Remote("dev"), Remote("kit/hosted")], root).Ok);
        // And his note is still there: a merge, never a reset onto dev.
        Assert.Contains("kit: leave a note (kit)", Git(root, "--git-dir", bare, "log", "--format=%s", "kit/hosted"));
        Assert.Equal("kit: bring kit/hosted up to date with dev", Git(pod, "log", "-1", "--format=%s"));
    }

    [Fact]
    public void Already_up_to_date_writes_nothing()
    {
        var before = Remote("kit/hosted");

        var r = Store().CatchUp(pod);

        Assert.False(r.Committed);
        Assert.Equal("kit/hosted already has everything on dev", r.Reason);
        Assert.Equal(before, Remote("kit/hosted"));
    }

    [Fact]
    public void A_conflict_is_aborted_and_the_branch_left_exactly_as_it_was()
    {
        Commit(pod, "kit.beh", "behaviour BEH-1 \"edited in Kit\"\n", "kit: retitle BEH-1 (kit)");
        Git(pod, "push", "-q", "origin", "HEAD:kit/hosted");
        Commit(dev, "kit.beh", "behaviour BEH-1 \"edited on dev\"\n", "spec: retitle on dev");
        Git(dev, "push", "-q", "origin", "HEAD:dev");
        var head = Git(pod, "rev-parse", "HEAD");
        var remote = Remote("kit/hosted");

        var r = Store().CatchUp(pod);

        Assert.False(r.Committed);
        Assert.StartsWith("dev does not merge cleanly into kit/hosted, so it was left as it was", r.Reason);
        Assert.Equal(head, Git(pod, "rev-parse", "HEAD"));
        Assert.Equal(remote, Remote("kit/hosted"));
        // No merge in progress and no conflict markers: the next edit commits as if nothing happened.
        Assert.Equal(string.Empty, Git(pod, "status", "--porcelain"));
        Assert.False(File.Exists(Path.Combine(pod, ".git", "MERGE_HEAD")));
    }

    [Fact]
    public void It_never_touches_a_clone_that_is_on_the_base_itself()
    {
        Commit(dev, "kit-ui.beh", "behaviour BEH-2 \"two\"\n", "spec");

        var r = Store().CatchUp(dev);

        Assert.False(r.Committed);
        Assert.Equal("the clone is on dev, not an edits branch, so it is left alone", r.Reason);
    }

    [Theory]
    [InlineData(false, "dev", "git write-back is off, so there is no edits branch to bring up to date")]
    [InlineData(true, null, "no base branch (KIT_GIT_BASE), so there is nothing to catch up with")]
    public void Off_or_without_a_base_it_does_nothing_and_says_why(bool enabled, string? baseBranch, string reason)
    {
        Commit(dev, "kit-ui.beh", "behaviour BEH-2 \"two\"\n", "spec");
        Git(dev, "push", "-q", "origin", "HEAD:dev");
        var before = Remote("kit/hosted");

        var r = Store(enabled, baseBranch).CatchUp(pod);

        Assert.False(r.Committed);
        Assert.Equal(reason, r.Reason);
        Assert.Equal(before, Remote("kit/hosted"));
    }
}
