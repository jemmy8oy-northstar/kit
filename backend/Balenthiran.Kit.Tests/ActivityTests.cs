using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-ACTIVITY-1: an activity page lists what changed recently, newest first. An action is a
/// commit that touched a corpus or its notes; a project read from GitHub with no local clone has no
/// history here and is listed as such rather than shown as quiet.
/// </summary>
public class ActivityTests
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T3 = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Commits_to_corpora_and_notes_are_listed_newest_first_with_their_subjects()
    {
        var root = Repo();
        try
        {
            Commit(root, "alpha.beh", "behaviour BEH-A \"a\"\n", "add alpha", T1);
            Commit(root, "beta.beh", "behaviour BEH-B \"b\"\n", "add beta", T2);
            Commit(root, "alpha.notes.md", "# notes\n", "note on alpha", T3);
            Commit(root, "unrelated.txt", "x", "not corpus", T3.AddHours(1));

            var feed = new ActivityReader(new CorpusDirectory(root, root), new GitCorpusHistory()).Recent();

            Assert.Equal(
                [("alpha", "note on alpha", "2026-10-03T09:00:00Z"), ("beta", "add beta", "2026-10-02T09:00:00Z"), ("alpha", "add alpha", "2026-10-01T09:00:00Z")],
                feed.Entries.Select(e => (e.App, e.Summary, e.At)));
            Assert.All(feed.Entries, e => Assert.Equal(7, e.Commit.Length));
            Assert.Empty(feed.NoHistory);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void A_pull_request_merge_reads_as_its_title_and_a_body_never_leaks_into_another_entry()
    {
        var root = Repo();
        try
        {
            Commit(root, "alpha.beh", "behaviour BEH-A \"a\"\n", "add alpha\n\nwhy, over\nseveral lines", T1);
            Commit(root, "alpha.notes.md", "# notes\n", "Merge pull request #348 from org/feat/epic-page\n\nAn epic page", T2);
            Commit(root, "beta.beh", "behaviour BEH-B \"b\"\n", "Merge pull request #343 from org/feat/session-survives-restart", T3);

            var feed = new ActivityReader(new CorpusDirectory(root, root), new GitCorpusHistory()).Recent();

            Assert.Equal(
                ["session survives restart (#343)", "An epic page (#348)", "add alpha"],
                feed.Entries.Select(e => e.Summary));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void The_feed_is_capped_at_its_newest_entries()
    {
        var root = Repo();
        try
        {
            for (var i = 0; i < ActivityReader.Cap + 3; i++)
            {
                Commit(root, "alpha.beh", $"behaviour BEH-A \"{i}\"\n", $"edit {i}", T1.AddMinutes(i));
            }

            var feed = new ActivityReader(new CorpusDirectory(root, root), new GitCorpusHistory()).Recent();

            Assert.Equal(ActivityReader.Cap, feed.Entries.Count);
            Assert.Equal($"edit {ActivityReader.Cap + 2}", feed.Entries[0].Summary);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void A_project_with_no_history_here_is_listed_with_its_reason_not_dropped()
    {
        var root = Repo();
        try
        {
            Commit(root, "alpha.beh", "behaviour BEH-A \"a\"\n", "add alpha", T1);
            Commit(root, "beta.beh", "behaviour BEH-B \"b\"\n", "add beta", T2);

            var feed = new ActivityReader(new CorpusDirectory(root, root), new Blind("beta")).Recent();

            Assert.Equal("alpha", Assert.Single(feed.Entries).App);
            var gap = Assert.Single(feed.NoHistory);
            Assert.Equal("beta", gap.App);
            Assert.Contains("not on this server's disk", gap.Reason);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void A_corpus_not_on_disk_or_outside_a_work_tree_says_why()
    {
        var root = Repo();
        var bare = Path.Combine(Path.GetTempPath(), "kit-noact-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(bare);
        try
        {
            var ghost = new GitCorpusHistory().Recent(Path.Combine(root, "ghost.beh"), Path.Combine(root, "ghost.notes.md"), 5);
            Assert.Contains("not on this server's disk", ghost.Reason);

            File.WriteAllText(Path.Combine(bare, "demo.beh"), "behaviour BEH-A \"a\"\n");
            var loose = new GitCorpusHistory().Recent(Path.Combine(bare, "demo.beh"), Path.Combine(bare, "demo.notes.md"), 5);
            Assert.Equal("the corpus is not in a git work tree", loose.Reason);
            Assert.Empty(loose.Commits);
        }
        finally
        {
            Delete(root);
            Delete(bare);
        }
    }

    [Fact]
    public void The_route_serves_the_feed()
    {
        var root = Repo();
        try
        {
            Commit(root, "demo.beh", "behaviour BEH-A \"a\"\n", "add demo", T1);
            var corpora = new CorpusDirectory(root, root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            var router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), null);

            var ok = router.Route("GET", "/api/activity");

            Assert.Equal(200, ok.Status);
            var body = JsonSerializer.Serialize(ok.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            using var doc = JsonDocument.Parse(body);
            var entry = doc.RootElement.GetProperty("entries")[0];
            Assert.Equal("demo", entry.GetProperty("app").GetString());
            Assert.Equal("add demo", entry.GetProperty("summary").GetString());
            Assert.Equal("2026-10-01T09:00:00Z", entry.GetProperty("at").GetString());
            Assert.Equal(0, doc.RootElement.GetProperty("noHistory").GetArrayLength());
        }
        finally
        {
            Delete(root);
        }
    }

    private static string Repo()
    {
        var root = Path.Combine(Path.GetTempPath(), "kit-act-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Git(root, null, "init", "-q", "-b", "main");
        Git(root, null, "config", "user.name", "t");
        Git(root, null, "config", "user.email", "t@t");
        Git(root, null, "config", "commit.gpgsign", "false");
        return root;
    }

    private static void Commit(string root, string name, string text, string subject, DateTimeOffset at)
    {
        File.WriteAllText(Path.Combine(root, name), text);
        Git(root, null, "add", name);
        Git(root, at, "commit", "-q", "-m", subject);
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

    /// <summary>Git history, except for one app standing in for a project read from GitHub with no clone.</summary>
    private sealed class Blind(string app) : ICorpusHistory
    {
        private readonly GitCorpusHistory real = new();

        public ICommitHeadlines Recent(string file, string notes, int limit) =>
            Path.GetFileName(file) == app + ".beh"
                ? real.Recent(Path.Combine(Path.GetDirectoryName(file)!, "ghost.beh"), notes, limit)
                : real.Recent(file, notes, limit);

        public ICommitLog Log(string file) => real.Log(file);

        public string? Text(string file, string commit) => real.Text(file, commit);
    }
}
