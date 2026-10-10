using System.Text.Json;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-NOTE-1..3 (kit#118): free text left in Kit, appended to <c>&lt;app&gt;.notes.md</c> beside the
/// corpus, committed like any edit, and listed back. The corpus itself is never touched.
/// </summary>
public class NotesTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 10, 20, 41, 37, TimeSpan.Zero);

    private const string Corpus = "behaviour BEH-B2 \"second\"\n  source defined\n  when opens page:Home\n";

    // ---- the file ---------------------------------------------------------------------------

    [Fact]
    public void The_first_note_writes_the_header_and_a_dated_heading()
    {
        Assert.Equal(
            "# Notes on demo\n\nLeft in Kit (kit#118). Fold each one into the spec or the code, and delete it in the same pull request.\n"
            + "\n## 2026-10-10 20:41Z\n\nthe list should keep its order\n",
            Notes.Append(null, "demo", "  the list should keep its order\n\n", null, At));
    }

    [Fact]
    public void A_second_note_is_appended_and_both_parse_back_oldest_first()
    {
        var one = Notes.Append(null, "demo", "first", null, At);
        var two = Notes.Append(one, "demo", "second\n\nwith a gap", "BEH-B2", At.AddHours(1));

        Assert.StartsWith(one, two);
        var notes = Notes.Parse(two);
        Assert.Equal(2, notes.Count);
        Assert.Equal(("2026-10-10 20:41Z", (string?)null, "first"), (notes[0].At, notes[0].Behaviour, notes[0].Text));
        Assert.Equal(("2026-10-10 21:41Z", "BEH-B2", "second\n\nwith a gap"), (notes[1].At, notes[1].Behaviour, notes[1].Text));
    }

    /// <summary>A note that types a heading, or what an escaped heading looks like, comes back as it was typed — one note, not two.</summary>
    [Fact]
    public void A_note_that_looks_like_a_heading_cannot_split_itself_and_round_trips()
    {
        var typed = "## 2026-01-01 00:00Z · BEH-FORGED\n\\## already escaped\n\\\\##two\n##plain";

        var notes = Notes.Parse(Notes.Append(null, "demo", typed, null, At));

        Assert.Equal(typed, Assert.Single(notes).Text);
    }

    [Fact]
    public void Windows_line_endings_are_stored_as_newlines()
    {
        Assert.Equal("a\nb", Assert.Single(Notes.Parse(Notes.Append(null, "demo", "a\r\nb", null, At))).Text);
    }

    [Fact]
    public void No_file_is_no_notes_and_the_header_alone_is_no_notes()
    {
        Assert.Empty(Notes.Parse(null));
        Assert.Empty(Notes.Parse(Notes.Header("demo")));
    }

    // ---- the routes -------------------------------------------------------------------------

    private sealed class Rig : IDisposable
    {
        public readonly string Root = Directory.CreateTempSubdirectory("kit-notes-").FullName;
        public KitRouter Router;

        public Rig(string? password = null)
        {
            System.IO.File.WriteAllText(Beh, Corpus);
            var corpora = new CorpusDirectory(Root, Root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            Router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(Root, "no-bundle")), password, clock: new Fixed());
        }

        public string Beh => Path.Combine(Root, "demo.beh");

        public string NotesFile => Path.Combine(Root, "demo.notes.md");

        public Balenthiran.Kit.Abstractions.DataModels.IKitResponse Post(string path, string body) =>
            Router.Route("POST", path, body: JsonDocument.Parse(body).RootElement);

        public JsonElement Get(string path) => JsonSerializer.SerializeToElement(Router.Route("GET", path).Body, Web);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed class Fixed : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => At;
    }

    [Fact]
    public void A_note_is_written_beside_the_corpus_and_the_corpus_is_byte_identical()
    {
        using var rig = new Rig();

        var r = rig.Post("/api/projects/demo/notes", """{"text":"the habit list should remember its order","behaviour":"BEH-B2"}""");

        Assert.Equal(200, r.Status);
        Assert.Equal(Notes.Append(null, "demo", "the habit list should remember its order", "BEH-B2", At), File.ReadAllText(rig.NotesFile));
        Assert.Equal(Corpus, File.ReadAllText(rig.Beh));
        var body = JsonSerializer.SerializeToElement(r.Body, Web);
        Assert.Equal("demo.notes.md", body.GetProperty("file").GetString());
        Assert.Equal("BEH-B2", body.GetProperty("behaviour").GetString());
    }

    [Fact]
    public void The_notes_route_lists_what_was_left_oldest_first()
    {
        using var rig = new Rig();
        Assert.Empty(rig.Get("/api/projects/demo/notes").GetProperty("notes").EnumerateArray());

        rig.Post("/api/projects/demo/notes", """{"text":"one"}""");
        rig.Post("/api/projects/demo/notes", """{"text":"two","behaviour":"BEH-B2"}""");

        var list = rig.Get("/api/projects/demo/notes");
        Assert.Equal("demo", list.GetProperty("app").GetString());
        Assert.Equal("demo.notes.md", list.GetProperty("file").GetString());
        var notes = list.GetProperty("notes").EnumerateArray().ToList();
        Assert.Equal(["one", "two"], notes.Select(n => n.GetProperty("text").GetString()));
        Assert.Equal(JsonValueKind.Null, notes[0].GetProperty("behaviour").ValueKind);
        Assert.Equal("BEH-B2", notes[1].GetProperty("behaviour").GetString());
        Assert.Equal("2026-10-10 20:41Z", notes[1].GetProperty("at").GetString());
    }

    [Theory]
    [InlineData("""{"text":""}""", 400)]
    [InlineData("""{"text":"  \n\t "}""", 400)]
    [InlineData("""{"text":1}""", 400)]
    [InlineData("""{}""", 400)]
    [InlineData("""{"text":"x","behaviour":"BEH 1"}""", 400)]
    [InlineData("""{"text":"x","behaviour":"BEH-1\n## 2026-01-01 00:00Z"}""", 400)]
    [InlineData("""{"text":"x","behaviour":7}""", 400)]
    public void A_refused_note_answers_its_status_and_writes_nothing(string body, int status)
    {
        using var rig = new Rig();
        Assert.Equal(status, rig.Post("/api/projects/demo/notes", body).Status);
        Assert.False(File.Exists(rig.NotesFile));
    }

    [Fact]
    public void An_overlong_note_is_refused_with_its_length()
    {
        using var rig = new Rig();
        var r = rig.Post("/api/projects/demo/notes", JsonSerializer.Serialize(new { text = new string('x', Notes.MaxLength + 1) }));
        Assert.Equal(400, r.Status);
        Assert.Contains($"this one is {Notes.MaxLength + 1}", JsonSerializer.Serialize(r.Body, Web));
        Assert.False(File.Exists(rig.NotesFile));
    }

    [Fact]
    public void A_note_on_a_project_that_does_not_exist_is_a_404_both_ways()
    {
        using var rig = new Rig();
        Assert.Equal(404, rig.Post("/api/projects/ghost/notes", """{"text":"x"}""").Status);
        Assert.Equal(404, rig.Router.Route("GET", "/api/projects/ghost/notes").Status);
    }

    [Fact]
    public void A_locked_kit_refuses_an_unauthenticated_note_like_every_other_write()
    {
        using var rig = new Rig(password: "secret");
        Assert.Equal(401, rig.Post("/api/projects/demo/notes", """{"text":"x"}""").Status);
        Assert.False(File.Exists(rig.NotesFile));
    }

    [Fact]
    public void A_note_is_committed_and_pushed_with_a_message_naming_it()
    {
        var root = Directory.CreateTempSubdirectory("kit-notes-git-").FullName;
        try
        {
            var bare = Path.Combine(root, "remote.git");
            var clone = Path.Combine(root, "clone");
            GitStore.Git(["init", "-q", "--bare", "-b", "main", bare], root);
            GitStore.Git(["clone", "-q", bare, clone], root);
            var dir = Path.Combine(clone, "behaviours");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "demo.beh"), Corpus);
            GitStore.Git(["add", "-A"], clone);
            GitStore.Git(["-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "seed"], clone);
            GitStore.Git(["push", "-q", "origin", "HEAD:main"], clone);

            var corpora = new CorpusDirectory(dir, clone);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            var router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), null, git: new GitStore(enabled: true, branch: "main"));

            var r = router.Route("POST", "/api/projects/demo/notes", body: JsonDocument.Parse("""{"text":"fold me","behaviour":"BEH-B2"}""").RootElement);

            Assert.Equal(200, r.Status);
            Assert.Equal("kit: leave a note on BEH-B2 (demo)", GitStore.Git(["--git-dir", bare, "log", "-1", "--format=%s", "main"], root).Stdout.Trim());
            Assert.Contains("fold me", GitStore.Git(["--git-dir", bare, "show", "main:behaviours/demo.notes.md"], root).Stdout);
            Assert.True(JsonSerializer.SerializeToElement(r.Body, Web).GetProperty("pushed").GetBoolean());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
