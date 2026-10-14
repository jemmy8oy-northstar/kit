using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-BOARD-4: Done is ordered most recently completed first. "Completed is when the behaviour
/// last stopped being pending, read from git history, not stored in the .beh", and "a behaviour
/// with no history (never pending) is completed when it was added".
/// </summary>
public class CompletionTests
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T3 = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T4 = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The scan exists so an old commit in an older grammar still reads; it must never disagree
    /// with the parser on a corpus the parser accepts, or the board's columns (the parser's) and
    /// its order (the scan's) would describe two different corpora.
    /// </summary>
    [Fact]
    public void The_scan_agrees_with_the_parser_on_every_frozen_corpus()
    {
        // Frozen, not live (kit#182: a spec edit must never turn this red), plus the parser's own
        // pending edge cases.
        var files = Directory.GetFiles(RepoLayout.FrozenBehaviours, "*.beh")
            .Append(Path.Combine(RepoLayout.Root, "backend", "Balenthiran.Kit.Tests", "Fixtures", "pending-edges.beh"))
            .ToList();
        Assert.NotEmpty(files);
        var pendingSeen = 0;
        foreach (var f in files)
        {
            var text = File.ReadAllText(f);
            var parsed = new CorpusParser().Parse(text, f).Select(b => new KeyValuePair<string, bool>(b.Id, b.Pending)).ToList();
            Assert.Equal(parsed, Completions.Scan(text));
            pendingSeen += parsed.Count(p => p.Value);
        }

        // A corpus set with no pending behaviour would let a scan that never says pending pass.
        Assert.True(pendingSeen > 0);
    }

    [Fact]
    public void The_scan_reads_pending_by_the_parsers_rules_not_by_indentation()
    {
        var text = "# kit:layer technical\nbehaviour BEH-A \"a\"\npending\n  # pending\nbehaviour BEH-B \"b\"\n  actor x\nfeature FEAT-X \"x\"\n  pending\nbehaviour BEH-C \"c\"\n\t pending \r\n";
        Assert.Equal(
            [new("BEH-A", true), new("BEH-B", false), new("BEH-C", true)],
            Completions.Scan(text));
    }

    [Fact]
    public void Completed_is_when_it_last_stopped_being_pending()
    {
        var done = Completions.Of(
            Now(("BEH-A", false)),
            [V(T1, ("BEH-A", true)), V(T2, ("BEH-A", false)), V(T3, ("BEH-A", true)), V(T4, ("BEH-A", false))]);

        Assert.Equal("2026-10-04T09:00:00Z", Assert.Single(done).At);
    }

    [Fact]
    public void A_later_edit_that_leaves_it_built_does_not_move_its_completion()
    {
        var done = Completions.Of(
            Now(("BEH-A", false)),
            [V(T1, ("BEH-A", true)), V(T2, ("BEH-A", false)), V(T3, ("BEH-A", false), ("BEH-B", true))]);

        Assert.Equal("2026-10-02T09:00:00Z", Assert.Single(done).At);
    }

    [Fact]
    public void A_behaviour_never_pending_is_completed_when_it_was_added()
    {
        var done = Completions.Of(
            Now(("BEH-A", false), ("BEH-B", false)),
            [V(T1, ("BEH-A", false)), V(T3, ("BEH-A", false), ("BEH-B", false))]);

        Assert.Equal(["2026-10-01T09:00:00Z", "2026-10-03T09:00:00Z"], done.Select(c => c.At));
    }

    /// <summary>
    /// Built as served but pending (or absent) at the newest commit: an uncommitted edit, or a pod's
    /// clone behind GitHub's read. It is the NEWEST completion, so it must not be dated by the
    /// history it is ahead of — the add time would sink it to the bottom of Done.
    /// </summary>
    [Fact]
    public void Built_ahead_of_the_history_has_no_date_rather_than_an_old_one()
    {
        var done = Completions.Of(
            Now(("BEH-A", false), ("BEH-B", false)),
            [V(T1, ("BEH-A", true))]);

        Assert.Equal([null, null], done.Select(c => c.At));
    }

    [Fact]
    public void Pending_behaviours_are_not_completed_and_the_corpus_order_is_kept()
    {
        var done = Completions.Of(
            Now(("BEH-B", false), ("BEH-P", true), ("BEH-A", false)),
            [V(T1, ("BEH-A", false), ("BEH-B", false), ("BEH-P", true))]);

        Assert.Equal(["BEH-B", "BEH-A"], done.Select(c => c.Id));
    }

    [Fact]
    public void Deleted_while_pending_and_restored_built_is_completed_on_its_return()
    {
        var done = Completions.Of(
            Now(("BEH-A", false)),
            [V(T1, ("BEH-A", true)), V(T2), V(T3, ("BEH-A", false))]);

        // The gap (a deleted file, or a version without it) is not a version in which it was built.
        Assert.Equal("2026-10-03T09:00:00Z", Assert.Single(done).At);
    }

    // ── git ──────────────────────────────────────────────────────────────

    [Fact]
    public void Git_history_is_the_first_parent_line_oldest_first_with_a_merge_dated_when_it_landed()
    {
        var root = Repo();
        try
        {
            var file = Path.Combine(root, "demo.beh");
            Commit(root, "behaviour BEH-A \"a\"\n  pending\n", T1);
            Git(root, null, "checkout", "-q", "-b", "side");
            Commit(root, "behaviour BEH-A \"a\"\n", T2);
            Git(root, null, "checkout", "-q", "main");
            Git(root, T4, "merge", "-q", "--no-ff", "-m", "merge side", "side");

            var log = new GitCorpusHistory().Log(file);
            Assert.Null(log.Reason);
            Assert.Equal([T1, T4], log.Commits.Select(c => c.At));

            var reader = new CompletionReader(new CorpusDirectory(root, root), new GitCorpusHistory());
            var list = reader.For("demo");
            Assert.Equal("git", list.History);
            Assert.Equal("demo.beh", list.File);
            Assert.Equal("2026-10-04T09:00:00Z", Assert.Single(list.Completed).At);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void An_uncommitted_edit_that_builds_a_behaviour_is_the_newest_completion()
    {
        var root = Repo();
        try
        {
            Commit(root, "behaviour BEH-A \"a\"\n  pending\nbehaviour BEH-B \"b\"\n", T1);
            File.WriteAllText(Path.Combine(root, "demo.beh"), "behaviour BEH-A \"a\"\nbehaviour BEH-B \"b\"\n");

            var list = new CompletionReader(new CorpusDirectory(root, root), new GitCorpusHistory()).For("demo");
            Assert.Equal([("BEH-A", (string?)null), ("BEH-B", "2026-10-01T09:00:00Z")], list.Completed.Select(c => (c.Id, c.At)));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void A_corpus_outside_a_work_tree_has_no_history_and_says_why()
    {
        var root = Path.Combine(Path.GetTempPath(), "kit-nohist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "demo.beh"), "behaviour BEH-A \"a\"\n");
            var list = new CompletionReader(new CorpusDirectory(root, root), new GitCorpusHistory()).For("demo");
            Assert.Equal("none", list.History);
            Assert.Equal("the corpus is not in a git work tree", list.Reason);
            Assert.Empty(list.Completed);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void A_corpus_not_on_disk_has_no_history_rather_than_an_empty_one()
    {
        var root = Repo();
        try
        {
            var log = new GitCorpusHistory().Log(Path.Combine(root, "ghost.beh"));
            Assert.NotNull(log.Reason);
            Assert.Contains("not on this server's disk", log.Reason);
        }
        finally
        {
            Delete(root);
        }
    }

    /// <summary>A commit is scanned once: the second read asks git for the log, not the text again.</summary>
    [Fact]
    public void Each_commit_is_read_once()
    {
        var history = new Counting();
        var dir = new FakeCorpora("behaviour BEH-A \"a\"\n");
        var reader = new CompletionReader(dir, history);
        reader.For("demo");
        reader.For("demo");
        Assert.Equal(2, history.Logs);
        Assert.Equal(1, history.Texts);
    }

    /// <summary>
    /// A git failure reads as an empty version for that one answer, and is asked again next time:
    /// cached, it would misdate the behaviours around that commit until a restart.
    /// </summary>
    [Fact]
    public void A_commit_git_could_not_show_is_not_kept()
    {
        var history = new Counting { FailFirst = true };
        var reader = new CompletionReader(new FakeCorpora("behaviour BEH-A \"a\"\n"), history);

        Assert.Null(Assert.Single(reader.For("demo").Completed).At);
        Assert.Equal("2026-10-01T09:00:00Z", Assert.Single(reader.For("demo").Completed).At);
        Assert.Equal(2, history.Texts);
    }

    // ── the route ────────────────────────────────────────────────────────

    [Fact]
    public void The_route_serves_the_history_and_refuses_an_unknown_project()
    {
        var root = Repo();
        try
        {
            Commit(root, "behaviour BEH-A \"a\"\n", T1);
            var corpora = new CorpusDirectory(root, root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            var router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), null);

            var ok = router.Route("GET", "/api/projects/demo/history");
            Assert.Equal(200, ok.Status);
            var body = JsonSerializer.Serialize(ok.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.Equal("{\"app\":\"demo\",\"file\":\"demo.beh\",\"history\":\"git\",\"reason\":null,\"completed\":[{\"id\":\"BEH-A\",\"at\":\"2026-10-01T09:00:00Z\"}]}", body);

            Assert.Equal(404, router.Route("GET", "/api/projects/ghost/history").Status);
            Assert.Equal(400, router.Route("GET", "/api/projects/%E0%A4%A/history").Status);
        }
        finally
        {
            Delete(root);
        }
    }

    // ── rig ──────────────────────────────────────────────────────────────

    private static List<KeyValuePair<string, bool>> Now(params (string Id, bool Pending)[] b) =>
        b.Select(x => new KeyValuePair<string, bool>(x.Id, x.Pending)).ToList();

    private static ICorpusVersion V(DateTimeOffset at, params (string Id, bool Pending)[] b) =>
        new CorpusVersion { At = at, Pending = Now(b) };

    private static string Repo()
    {
        var root = Path.Combine(Path.GetTempPath(), "kit-hist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Git(root, null, "init", "-q", "-b", "main");
        Git(root, null, "config", "user.name", "t");
        Git(root, null, "config", "user.email", "t@t");
        Git(root, null, "config", "commit.gpgsign", "false");
        return root;
    }

    private static void Commit(string root, string text, DateTimeOffset at)
    {
        File.WriteAllText(Path.Combine(root, "demo.beh"), text);
        Git(root, null, "add", "demo.beh");
        Git(root, at, "commit", "-q", "-m", "edit");
    }

    private static void Git(string root, DateTimeOffset? at, params string[] args)
    {
        var stamp = at?.ToString("yyyy-MM-ddTHH:mm:sszzz");
        var env = stamp is null ? null : new Dictionary<string, string> { ["GIT_COMMITTER_DATE"] = stamp, ["GIT_AUTHOR_DATE"] = stamp };
        var r = GitStore.Git(args, root, env);
        Assert.True(r.Ok, r.Failure);
    }

    private static void Delete(string root)
    {
        foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(f, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }

    private sealed class Counting : Abstractions.Services.ICorpusHistory
    {
        public int Logs { get; private set; }

        public int Texts { get; private set; }

        public ICommitLog Log(string file)
        {
            Logs++;
            return new CommitLog { Reason = null, Commits = [new CommitStamp { Commit = "c1", At = T1 }] };
        }

        public bool FailFirst { get; init; }

        public string? Text(string file, string commit)
        {
            Texts++;
            return FailFirst && Texts == 1 ? null : "behaviour BEH-A \"a\"\n";
        }
    }

    private sealed class FakeCorpora(string text) : Abstractions.Services.ICorpusDirectory
    {
        public IReadOnlyList<string> Corpora() => ["demo"];
        public string Read(string app) => text;
        public System.Text.Json.Nodes.JsonObject Bindings(string app) => [];
        public string RelativePath(string app) => "demo.beh";
        public string RelativeBindingsPath(string app) => throw new NotSupportedException();
        public string FullPath(string app) => "/nowhere/demo.beh";
        public string FullBindingsPath(string app) => throw new NotSupportedException();
        public string ReadText(string app) => text;
        public string? ReadBindingsText(string app) => throw new NotSupportedException();
        public void WriteText(string app, string text) => throw new NotSupportedException();
        public void WriteBindingsText(string app, string text) => throw new NotSupportedException();
        public string? ReadNotesText(string app) => throw new NotSupportedException();
        public void WriteNotesText(string app, string text) => throw new NotSupportedException();
        public string RelativeNotesPath(string app) => throw new NotSupportedException();
        public string FullNotesPath(string app) => throw new NotSupportedException();
        public string? ReadScheduleText(string app) => throw new NotSupportedException();
        public void WriteScheduleText(string app, string text) => throw new NotSupportedException();
        public string RelativeSchedulePath(string app) => throw new NotSupportedException();
        public string FullSchedulePath(string app) => throw new NotSupportedException();
    }
}
