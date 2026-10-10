using System.Text.Json;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>BEH-ACT-2: an existing behaviour is updated without rewriting the file around it.</summary>
public class UpdateBehaviourTests
{
    private static readonly CorpusWriter Writer = new(new CorpusParser());

    private const string Corpus =
        "# header comment\n" +
        "\n" +
        "behaviour A-1 \"first\"\n" +
        "  source defined\n" +
        "  when opens page:Home\n" +
        "\n" +
        "# about B\n" +
        "behaviour BEH-B2 \"second\"\n" +
        "  source defined\n" +
        "  # inner comment\n" +
        "  when opens page:Home\n" +
        "    then sees region:List\n" +
        "  contract one line of why\n" +
        "\n" +
        "behaviour C-3 \"third\"\n" +
        "  source defined\n" +
        "  when opens page:Home\n";

    // ---- UpdateStep -------------------------------------------------------------------------

    [Fact]
    public void A_step_is_replaced_in_place_and_every_other_byte_survives()
    {
        var r = Writer.UpdateStep(Corpus, "BEH-B2", 0, "when opens page:Settings");
        Assert.True(r.Ok, r.Reason);
        Assert.Equal(Corpus.Replace("  # inner comment\n  when opens page:Home\n", "  # inner comment\n  when opens page:Settings\n", StringComparison.Ordinal), r.Text);
    }

    [Fact]
    public void The_index_counts_steps_only_and_the_line_keeps_its_own_indentation()
    {
        var r = Writer.UpdateStep(Corpus, "BEH-B2", 1, "  then sees region:Grid  ");
        Assert.True(r.Ok, r.Reason);
        Assert.Contains("\n    then sees region:Grid\n", r.Text);
        Assert.Equal(Corpus.Length - "List".Length + "Grid".Length, r.Text!.Length);
    }

    [Fact]
    public void A_contract_line_is_a_step_and_can_be_reworded()
    {
        var r = Writer.UpdateStep(Corpus, "BEH-B2", 2, "contract a better reason");
        Assert.True(r.Ok, r.Reason);
        Assert.Contains("\n  contract a better reason\n", r.Text);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void An_index_past_either_end_is_refused_with_the_step_count(int index)
    {
        var r = Writer.UpdateStep(Corpus, "BEH-B2", index, "when opens page:Home");
        Assert.False(r.Ok);
        Assert.Equal("no-such-step", r.Error);
        Assert.Contains("3 step(s)", r.Reason);
    }

    [Theory]
    [InlineData("   ", "empty-step")]
    [InlineData("when opens page:Home\n  review approved", "multiline-step")]
    public void A_blank_or_multiline_step_is_refused(string line, string error)
    {
        var r = Writer.UpdateStep(Corpus, "BEH-B2", 0, line);
        Assert.False(r.Ok);
        Assert.Equal(error, r.Error);
    }

    /// <summary>Rule 3 exempts the target, so this is the guard that stops a step box approving its own behaviour.</summary>
    [Theory]
    [InlineData("review approved")]
    [InlineData("serves C-3")]
    [InlineData("actor someone")]
    public void A_line_that_is_not_a_step_is_refused_even_though_it_parses(string line)
    {
        var r = Writer.UpdateStep(Corpus, "BEH-B2", 0, line);
        Assert.False(r.Ok);
        Assert.True(r.Error == "not-a-step", r.Reason);
    }

    [Fact]
    public void A_crlf_corpus_keeps_its_line_endings()
    {
        var crlf = Corpus.Replace("\n", "\r\n", StringComparison.Ordinal);
        var r = Writer.UpdateStep(crlf, "A-1", 0, "when opens page:Settings");
        Assert.True(r.Ok, r.Reason);
        Assert.Equal(crlf.Replace("behaviour A-1 \"first\"\r\n  source defined\r\n  when opens page:Home\r\n", "behaviour A-1 \"first\"\r\n  source defined\r\n  when opens page:Settings\r\n", StringComparison.Ordinal), r.Text);
    }

    [Fact]
    public void Updating_a_step_of_an_unknown_behaviour_lists_the_known_ids()
    {
        var r = Writer.UpdateStep(Corpus, "NOPE-1", 0, "when opens page:Home");
        Assert.Equal("no-such-behaviour", r.Error);
        Assert.Equal(["A-1", "BEH-B2", "C-3"], r.Known!.ToArray());
    }

    [Fact]
    public void A_corpus_that_does_not_parse_is_refused_not_edited()
    {
        Assert.Equal("corpus-already-invalid", Writer.UpdateStep("behaviour A-1 \"x\"\n  when opens page:Home\n  bogus line\n", "A-1", 0, "when opens page:Settings").Error);
        Assert.Equal("corpus-already-invalid", Writer.Retitle("behaviour A-1 \"x\"\n  bogus line\n", "A-1", "y").Error);
    }

    // ---- Retitle ----------------------------------------------------------------------------

    [Fact]
    public void A_retitle_changes_only_the_text_between_the_quotes()
    {
        var r = Writer.Retitle(Corpus, "BEH-B2", "  the second, renamed ");
        Assert.True(r.Ok, r.Reason);
        Assert.Equal(Corpus.Replace("behaviour BEH-B2 \"second\"", "behaviour BEH-B2 \"the second, renamed\"", StringComparison.Ordinal), r.Text);
    }

    /// <summary>The parser's title runs to the last quote; stopping at the second would leave <c>hi" there</c> behind.</summary>
    [Fact]
    public void A_hand_written_title_holding_quotes_is_replaced_whole()
    {
        var text = "behaviour Q-1 \"say \"hi\" there\"\n  source defined\n  when opens page:Home\n";
        var r = Writer.Retitle(text, "Q-1", "greet");
        Assert.True(r.Ok, r.Reason);
        Assert.Equal("behaviour Q-1 \"greet\"\n  source defined\n  when opens page:Home\n", r.Text);
        Assert.Equal("greet", new CorpusParser().Parse(r.Text!, "after").Single().Title);
    }

    [Theory]
    [InlineData("say \"hi\"")]
    [InlineData("two\nlines")]
    [InlineData("  ")]
    public void A_title_with_a_quote_a_newline_or_nothing_is_refused(string title)
    {
        var r = Writer.Retitle(Corpus, "BEH-B2", title);
        Assert.False(r.Ok);
        Assert.Equal("bad-title", r.Error);
    }

    [Fact]
    public void Retitling_an_unknown_behaviour_is_refused()
    {
        Assert.Equal("no-such-behaviour", Writer.Retitle(Corpus, "NOPE-1", "x").Error);
    }

    // ---- the routes -------------------------------------------------------------------------

    private sealed class Rig : IDisposable
    {
        private readonly string root = Directory.CreateTempSubdirectory("kit-update-").FullName;
        public string File => Path.Combine(root, "demo.beh");
        public KitRouter Router;

        public Rig(string text, string? password = null)
        {
            System.IO.File.WriteAllText(File, text);
            var corpora = new CorpusDirectory(root, root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            Router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), password);
        }

        public Balenthiran.Kit.Abstractions.DataModels.IKitResponse Post(string path, string body) =>
            Router.Route("POST", path, body: JsonDocument.Parse(body).RootElement);

        public void Dispose() => Directory.Delete(root, recursive: true);
    }

    private const string StepPath = "/api/projects/demo/behaviours/BEH-B2/steps/0";
    private const string TitlePath = "/api/projects/demo/behaviours/BEH-B2/title";

    [Fact]
    public void The_step_route_writes_the_writers_text_to_disk()
    {
        using var rig = new Rig(Corpus);
        Assert.Equal(200, rig.Post(StepPath, """{"step":"when opens page:Settings"}""").Status);
        Assert.Equal(Writer.UpdateStep(Corpus, "BEH-B2", 0, "when opens page:Settings").Text, System.IO.File.ReadAllText(rig.File));
    }

    [Fact]
    public void The_title_route_writes_the_writers_text_to_disk()
    {
        using var rig = new Rig(Corpus);
        Assert.Equal(200, rig.Post(TitlePath, """{"title":"renamed"}""").Status);
        Assert.Equal(Writer.Retitle(Corpus, "BEH-B2", "renamed").Text, System.IO.File.ReadAllText(rig.File));
    }

    [Fact]
    public void Adding_a_step_is_still_an_append_and_not_an_update()
    {
        using var rig = new Rig(Corpus);
        Assert.Equal(200, rig.Post("/api/projects/demo/behaviours/A-1/steps", """{"step":"then sees region:List"}""").Status);
        Assert.Equal(Writer.AddStep(Corpus, "A-1", "then sees region:List").Text, System.IO.File.ReadAllText(rig.File));
    }

    [Theory]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/steps/x", """{"step":"when opens page:Home"}""", 400)]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/steps/-1", """{"step":"when opens page:Home"}""", 400)]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/steps/99999999999", """{"step":"when opens page:Home"}""", 400)]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/steps/3", """{"step":"when opens page:Home"}""", 409)]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/steps/0", """{"step":"review approved"}""", 409)]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/steps/0", """{"step":1}""", 400)]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/steps/0", """{}""", 400)]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/title", """{"title":null}""", 400)]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/title", """{"title":"a \"quote\""}""", 409)]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/review/0", """{"state":"approved"}""", 404)]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/title/0", """{"title":"x"}""", 404)]
    [InlineData("/api/projects/demo/behaviours/BEH-B2/remove/0", """{}""", 404)]
    [InlineData("/api/projects/ghost/behaviours/BEH-B2/title", """{"title":"x"}""", 404)]
    public void A_refused_update_answers_its_status_and_leaves_the_file_byte_identical(string path, string body, int status)
    {
        using var rig = new Rig(Corpus);
        Assert.Equal(status, rig.Post(path, body).Status);
        Assert.Equal(Corpus, System.IO.File.ReadAllText(rig.File));
    }

    [Fact]
    public void A_locked_kit_refuses_an_unauthenticated_update_like_every_other_write()
    {
        using var rig = new Rig(Corpus, password: "secret");
        Assert.Equal(401, rig.Post(StepPath, """{"step":"when opens page:Settings"}""").Status);
        Assert.Equal(401, rig.Post(TitlePath, """{"title":"renamed"}""").Status);
        Assert.Equal(Corpus, System.IO.File.ReadAllText(rig.File));
    }

    [Theory]
    [InlineData(StepPath, """{"step":"when opens page:Settings"}""", "update step 0 of BEH-B2", "when opens page:Settings")]
    [InlineData(TitlePath, """{"title":"renamed"}""", "retitle BEH-B2", "behaviour BEH-B2 \"renamed\"")]
    public void An_update_is_committed_and_pushed_with_a_message_naming_it(string path, string body, string message, string landed)
    {
        var root = Directory.CreateTempSubdirectory("kit-update-git-").FullName;
        try
        {
            var bare = Path.Combine(root, "remote.git");
            var clone = Path.Combine(root, "clone");
            GitStore.Git(["init", "-q", "--bare", "-b", "main", bare], root);
            GitStore.Git(["clone", "-q", bare, clone], root);
            var dir = Path.Combine(clone, "behaviours");
            Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(Path.Combine(dir, "demo.beh"), Corpus);
            GitStore.Git(["add", "-A"], clone);
            GitStore.Git(["-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "seed"], clone);
            GitStore.Git(["push", "-q", "origin", "HEAD:main"], clone);

            var corpora = new CorpusDirectory(dir, clone);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            var router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), null, git: new GitStore(enabled: true, branch: "main"));

            Assert.Equal(200, router.Route("POST", path, body: JsonDocument.Parse(body).RootElement).Status);
            Assert.Contains(message, GitStore.Git(["--git-dir", bare, "log", "-1", "--format=%s", "main"], root).Stdout);
            Assert.Contains(landed, GitStore.Git(["--git-dir", bare, "show", "main:behaviours/demo.beh"], root).Stdout);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Updates are read-modify-write on one file: without the edit lock a parallel one reverts another's.</summary>
    [Fact]
    public async Task Updates_made_at_the_same_moment_all_land()
    {
        const int n = 40;
        var text = string.Join("\n", Enumerable.Range(1, n).Select(i => $"behaviour U-{i} \"u{i}\"\n  source defined\n  when opens page:Home\n"));
        using var rig = new Rig(text);
        var answers = await Task.WhenAll(Enumerable.Range(1, n).Select(i => Task.Run(() => i % 2 == 0
            ? rig.Post($"/api/projects/demo/behaviours/U-{i}/steps/0", $$"""{"step":"when opens page:P{{i}}"}""")
            : rig.Post($"/api/projects/demo/behaviours/U-{i}/title", $$"""{"title":"t{{i}}"}"""))));
        Assert.All(answers, a => Assert.Equal(200, a.Status));
        var after = System.IO.File.ReadAllText(rig.File);
        Assert.All(Enumerable.Range(1, n), i => Assert.Contains(i % 2 == 0 ? $"page:P{i}\n" : $"\"t{i}\"", after));
    }
}
