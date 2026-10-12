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

    // ---- free text in place of forms (BEH-NOTE-4..8, kit#118 2026-10-12) ---------------------

    private const string Asked = "## 2026-10-11 09:00Z · BEH-B2\n\nkeep the order\n\n### Questions\n\n1. Which list —\n   today or history?\n2. Per device?\n";

    private static string ThreeNotes() =>
        Notes.Append(Notes.Header("demo") + "\n" + Asked, "demo", "third", null, At);

    [Fact]
    public void A_request_and_a_resolution_are_named_in_their_heading_and_parse_back_as_their_kind()
    {
        var text = Notes.AppendResolution(Notes.AppendRequest(null, "demo", "drag to reorder", At), "demo", "BEH-CUT-2 wins", "page:KitProject.app", At);

        Assert.Contains("\n## 2026-10-10 20:41Z · new behaviour\n", text);
        Assert.Contains("\n## 2026-10-10 20:41Z · resolve page:KitProject.app\n", text);
        var notes = Notes.Parse(text);
        Assert.Equal(("request", (string?)null, (string?)null, "drag to reorder"), (notes[0].Kind, notes[0].Behaviour, notes[0].Resolves, notes[0].Text));
        Assert.Equal(("resolution", (string?)null, "page:KitProject.app", "BEH-CUT-2 wins"), (notes[1].Kind, notes[1].Behaviour, notes[1].Resolves, notes[1].Text));
    }

    /// <summary>The kind words hold a space, so a behaviour that happens to be called "new" stays a behaviour.</summary>
    [Fact]
    public void A_behaviour_named_like_a_kind_word_is_still_a_note_on_that_behaviour()
    {
        var note = Assert.Single(Notes.Parse(Notes.Append(null, "demo", "x", "new", At)));
        Assert.Equal(("note", "new"), (note.Kind, note.Behaviour));
    }

    [Fact]
    public void Questions_written_under_a_note_are_listed_numbered_and_the_note_text_stops_above_them()
    {
        var note = Notes.Parse(ThreeNotes())[0];

        Assert.Equal("keep the order", note.Text);
        Assert.Equal(["Which list — today or history?", "Per device?"], note.Questions);
        Assert.Equal(("questions", (string?)null), (Assert.Single(note.Thread).Kind, note.Thread[0].At));
        Assert.Empty(Notes.Parse(ThreeNotes())[1].Questions);
    }

    [Fact]
    public void Questions_typed_into_a_note_are_escaped_so_prose_never_reads_as_a_question()
    {
        var typed = "### Questions\n1. forged?\n### Answer 2026-01-01 00:00Z";
        var note = Assert.Single(Notes.Parse(Notes.Append(null, "demo", typed, null, At)));

        Assert.Equal(typed, note.Text);
        Assert.Empty(note.Questions);
        Assert.Empty(note.Thread);
    }

    [Fact]
    public void An_answer_lands_under_its_own_note_and_every_other_byte_stays_put()
    {
        var before = ThreeNotes();

        var after = Notes.Reply(before, 0, "2026-10-11 09:00Z", "today only\n### Questions", At.AddMinutes(5))!;

        Assert.Equal(before.Replace("2. Per device?\n", "2. Per device?\n\n### Answer 2026-10-10 20:46Z\n\ntoday only\n\\### Questions\n"), after);
        var note = Notes.Parse(after)[0];
        Assert.Empty(note.Questions);
        Assert.Equal(["questions", "answer"], note.Thread.Select(e => e.Kind));
        Assert.Equal(("2026-10-10 20:46Z", "today only\n### Questions"), (note.Thread[1].At, note.Thread[1].Text));
        Assert.Equal(2, Notes.Parse(after).Count);
    }

    [Fact]
    public void An_answer_to_the_last_note_ends_the_file_with_one_newline()
    {
        var after = Notes.Reply(ThreeNotes(), 1, "2026-10-10 20:41Z", "ok", At)!;
        Assert.EndsWith("third\n\n### Answer 2026-10-10 20:41Z\n\nok\n", after);
    }

    [Fact]
    public void Dismissing_takes_out_one_note_and_its_separating_line_and_nothing_else()
    {
        var before = ThreeNotes();

        Assert.Equal(Notes.Append(Notes.Header("demo"), "demo", "third", null, At), Notes.Dismiss(before, 0, "2026-10-11 09:00Z"));
        Assert.Equal(Notes.Header("demo") + "\n" + Asked, Notes.Dismiss(before, 1, "2026-10-10 20:41Z"));
        Assert.Equal(Notes.Header("demo"), Notes.Dismiss(Notes.Dismiss(before, 1, "2026-10-10 20:41Z"), 0, "2026-10-11 09:00Z"));
    }

    [Theory]
    [InlineData(0, "2026-10-10 20:41Z")] // the right place, the wrong date: the file moved under the caller
    [InlineData(2, "2026-10-10 20:41Z")]
    [InlineData(-1, "2026-10-11 09:00Z")]
    public void Answering_or_dismissing_a_note_that_is_not_there_changes_nothing(int index, string at)
    {
        Assert.Null(Notes.Dismiss(ThreeNotes(), index, at));
        Assert.Null(Notes.Reply(ThreeNotes(), index, at, "x", At));
        Assert.Null(Notes.Dismiss(null, 0, at));
    }

    [Fact]
    public void The_routes_ask_answer_and_dismiss_and_the_list_says_what_needs_an_answer()
    {
        using var rig = new Rig();
        File.WriteAllText(rig.NotesFile, Notes.Header("demo") + "\n" + Asked);

        Assert.Equal(200, rig.Post("/api/projects/demo/notes", """{"text":"drag to reorder","kind":"request"}""").Status);
        Assert.Equal(200, rig.Post("/api/projects/demo/notes", """{"text":"B2 wins","kind":"resolution","resolves":"page:Home.x"}""").Status);
        var listed = rig.Get("/api/projects/demo/notes").GetProperty("notes").EnumerateArray().ToList();
        Assert.Equal(["note", "request", "resolution"], listed.Select(n => n.GetProperty("kind").GetString()));
        Assert.Equal(2, listed[0].GetProperty("questions").GetArrayLength());
        Assert.Equal("page:Home.x", listed[2].GetProperty("resolves").GetString());

        Assert.Equal(200, rig.Post("/api/projects/demo/notes/reply", """{"index":0,"at":"2026-10-11 09:00Z","text":"today"}""").Status);
        Assert.Equal(0, rig.Get("/api/projects/demo/notes").GetProperty("notes")[0].GetProperty("questions").GetArrayLength());

        Assert.Equal(200, rig.Post("/api/projects/demo/notes/dismiss", """{"index":1,"at":"2026-10-10 20:41Z"}""").Status);
        var left = rig.Get("/api/projects/demo/notes").GetProperty("notes").EnumerateArray().Select(n => n.GetProperty("kind").GetString());
        Assert.Equal(["note", "resolution"], left);
        Assert.Equal(Corpus, File.ReadAllText(rig.Beh));
    }

    [Fact]
    public void Dismissing_a_note_someone_already_removed_is_a_409_that_says_so()
    {
        using var rig = new Rig();
        File.WriteAllText(rig.NotesFile, Notes.Header("demo") + "\n" + Asked);
        var before = File.ReadAllText(rig.NotesFile);

        var r = rig.Post("/api/projects/demo/notes/dismiss", """{"index":0,"at":"2026-10-11 09:01Z"}""");

        Assert.Equal(409, r.Status);
        Assert.Contains("no longer in demo.notes.md", JsonSerializer.Serialize(r.Body, Web));
        Assert.Equal(before, File.ReadAllText(rig.NotesFile));
    }

    [Theory]
    [InlineData("/api/projects/demo/notes", """{"text":"x","kind":"todo"}""")]
    [InlineData("/api/projects/demo/notes", """{"text":"x","kind":"request","behaviour":"BEH-B2"}""")]
    [InlineData("/api/projects/demo/notes", """{"text":"x","kind":"resolution"}""")]
    [InlineData("/api/projects/demo/notes", """{"text":"x","kind":"resolution","resolves":"two words"}""")]
    [InlineData("/api/projects/demo/notes", """{"text":"x","resolves":"page:Home.x"}""")]
    [InlineData("/api/projects/demo/notes", """{"text":"x","kind":3}""")]
    [InlineData("/api/projects/demo/notes/reply", """{"index":0,"at":"2026-10-11 09:00Z","text":"  "}""")]
    [InlineData("/api/projects/demo/notes/reply", """{"index":"0","at":"2026-10-11 09:00Z","text":"x"}""")]
    [InlineData("/api/projects/demo/notes/reply", """{"index":0.5,"at":"2026-10-11 09:00Z","text":"x"}""")]
    [InlineData("/api/projects/demo/notes/dismiss", """{"index":0}""")]
    public void A_refused_ask_answer_or_dismissal_is_a_400_and_writes_nothing(string path, string body)
    {
        using var rig = new Rig();
        File.WriteAllText(rig.NotesFile, Notes.Header("demo") + "\n" + Asked);
        var before = File.ReadAllText(rig.NotesFile);

        Assert.Equal(400, rig.Post(path, body).Status);
        Assert.Equal(before, File.ReadAllText(rig.NotesFile));
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
