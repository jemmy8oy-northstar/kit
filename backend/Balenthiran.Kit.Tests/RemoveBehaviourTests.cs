using System.Text.Json;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>BEH-ACT-3: a behaviour is removed, and the removal is refused if anything still references it.</summary>
public class RemoveBehaviourTests
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
        "\n" +
        "# about C\n" +
        "behaviour C-3 \"third\"\n" +
        "  source defined\n" +
        "  when opens page:Home\n";

    [Fact]
    public void Removing_a_middle_block_takes_only_its_lines_and_one_separator()
    {
        var r = Writer.RemoveBehaviour(Corpus, "BEH-B2");
        Assert.True(r.Ok, r.Reason);
        Assert.Equal(
            "# header comment\n\nbehaviour A-1 \"first\"\n  source defined\n  when opens page:Home\n\n# about B\n# about C\nbehaviour C-3 \"third\"\n  source defined\n  when opens page:Home\n",
            r.Text);
    }

    [Fact]
    public void Removing_the_last_block_leaves_the_file_ending_in_one_newline_after_its_neighbour()
    {
        var r = Writer.RemoveBehaviour(Corpus, "C-3");
        Assert.True(r.Ok, r.Reason);
        Assert.Equal(Corpus[..Corpus.IndexOf("behaviour C-3", StringComparison.Ordinal)], r.Text);
        Assert.EndsWith("  when opens page:Home\n\n# about C\n", r.Text);
    }

    [Fact]
    public void Removing_the_first_block_keeps_the_comment_above_it()
    {
        var r = Writer.RemoveBehaviour(Corpus, "A-1");
        Assert.True(r.Ok, r.Reason);
        Assert.Equal(
            "# header comment\n\n# about B\nbehaviour BEH-B2 \"second\"\n  source defined\n  # inner comment\n  when opens page:Home\n\n# about C\nbehaviour C-3 \"third\"\n  source defined\n  when opens page:Home\n",
            r.Text);
    }

    [Fact]
    public void The_neighbours_survive_byte_for_byte_and_the_result_reparses()
    {
        var r = Writer.RemoveBehaviour(Corpus, "BEH-B2");
        var parsed = new CorpusParser().Parse(r.Text!, "after");
        Assert.Equal(["A-1", "C-3"], parsed.Select(b => b.Id).ToArray());
        Assert.Contains("behaviour A-1 \"first\"\n  source defined\n  when opens page:Home\n", r.Text);
        Assert.Contains("behaviour C-3 \"third\"\n  source defined\n  when opens page:Home\n", r.Text);
        Assert.Contains("# header comment", r.Text);
    }

    [Fact]
    public void Adding_then_removing_a_behaviour_returns_the_exact_original_text()
    {
        foreach (var start in new[] { Corpus, Corpus + "\n\n", "behaviour Z-9 \"only\"\n  source defined\n" })
        {
            var added = Writer.AddBehaviour(start, "NEW-1", "added", steps: ["when opens page:Home"]);
            Assert.True(added.Ok, added.Reason);
            var removed = Writer.RemoveBehaviour(added.Text!, "NEW-1");
            Assert.True(removed.Ok, removed.Reason);
            Assert.Equal(start.TrimEnd('\n') + "\n", removed.Text);

            // And again: the separator neither accumulates nor is eaten.
            var again = Writer.RemoveBehaviour(Writer.AddBehaviour(removed.Text!, "NEW-1", "added", steps: ["when opens page:Home"]).Text!, "NEW-1");
            Assert.Equal(removed.Text, again.Text);
        }
    }

    [Fact]
    public void An_unknown_id_is_refused_and_the_known_ids_are_listed()
    {
        var r = Writer.RemoveBehaviour(Corpus, "NOPE-1");
        Assert.False(r.Ok);
        Assert.Equal("no-such-behaviour", r.Error);
        Assert.Equal(["A-1", "BEH-B2", "C-3"], r.Known!.ToArray());
    }

    [Fact]
    public void A_behaviour_another_one_serves_is_refused_naming_the_referrer_and_its_line()
    {
        var text = Corpus + "\nbehaviour API-1 \"api\"\n  source inferred\n  serves BEH-B2\n  when opens page:Home\n";
        var r = Writer.RemoveBehaviour(text, "BEH-B2");
        Assert.False(r.Ok);
        Assert.True(r.Error == "still-referenced", r.Reason);
        Assert.Contains("API-1 (line 20)", r.Reason);
    }

    [Fact]
    public void A_behaviour_a_question_cites_is_refused_and_every_referrer_is_named()
    {
        var text = Corpus
            + "\nbehaviour Q-1 \"which\"\n  source inferred\n  asks \"pick\"\n  option \"x\" \"y\"\n  option \"z\" \"w\"\n  recommend \"x\" \"because\"\n  against \"no\"\n  cites BEH-B2\n"
            + "\nbehaviour API-1 \"api\"\n  source inferred\n  serves BEH-B2\n  when opens page:Home\n";
        var r = Writer.RemoveBehaviour(text, "BEH-B2");
        Assert.False(r.Ok);
        Assert.True(r.Error == "still-referenced", r.Reason);
        Assert.Contains("Q-1 (line 25)", r.Reason);
        Assert.Contains("API-1 (line 29)", r.Reason);
    }

    [Fact]
    public void A_behaviour_that_references_itself_is_not_blocked_by_it()
    {
        var text = Corpus + "\nbehaviour S-1 \"self\"\n  source inferred\n  serves S-1\n  when opens page:Home\n";
        var r = Writer.RemoveBehaviour(text, "S-1");
        Assert.True(r.Ok, r.Reason);
        Assert.DoesNotContain("S-1", r.Text);
    }

    [Fact]
    public void A_corpus_that_does_not_parse_is_refused_not_edited()
    {
        var r = Writer.RemoveBehaviour("behaviour A-1 \"x\"\n  bogus line\n", "A-1");
        Assert.False(r.Ok);
        Assert.Equal("corpus-already-invalid", r.Error);
    }

    // ---- the route -------------------------------------------------------------------------

    private sealed class Rig : IDisposable
    {
        private readonly string root = Directory.CreateTempSubdirectory("kit-remove-").FullName;
        public string File => Path.Combine(root, "demo.beh");
        public KitRouter Router;

        public Rig(string text, string? password = null, string host = "127.0.0.1")
        {
            System.IO.File.WriteAllText(File, text);
            var corpora = new CorpusDirectory(root, root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            Router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), password, host: host);
        }

        public Balenthiran.Kit.Abstractions.DataModels.IKitResponse Post(string path, string body = "{}") =>
            Router.Route("POST", path, body: JsonDocument.Parse(body).RootElement);

        public void Dispose() => Directory.Delete(root, recursive: true);
    }

    private const string Path1 = "/api/projects/demo/behaviours/BEH-B2/remove";

    [Fact]
    public void The_route_removes_the_block_from_the_file_on_disk()
    {
        using var rig = new Rig(Corpus);
        var r = rig.Post(Path1);
        Assert.Equal(200, r.Status);
        Assert.Equal(Writer.RemoveBehaviour(Corpus, "BEH-B2").Text, System.IO.File.ReadAllText(rig.File));
    }

    [Fact]
    public void A_refused_removal_is_409_and_leaves_the_file_byte_identical()
    {
        var text = Corpus + "\nbehaviour API-1 \"api\"\n  source inferred\n  serves BEH-B2\n  when opens page:Home\n";
        using var rig = new Rig(text);
        Assert.Equal(409, rig.Post(Path1).Status);
        Assert.Equal(text, System.IO.File.ReadAllText(rig.File));
        Assert.Equal(409, rig.Post("/api/projects/demo/behaviours/NOPE-1/remove").Status);
        Assert.Equal(text, System.IO.File.ReadAllText(rig.File));
    }

    [Fact]
    public void An_unknown_project_is_404()
    {
        using var rig = new Rig(Corpus);
        Assert.Equal(404, rig.Post("/api/projects/ghost/behaviours/BEH-B2/remove").Status);
    }

    [Fact]
    public void A_body_with_any_field_is_refused_and_nothing_is_written()
    {
        using var rig = new Rig(Corpus);
        Assert.Equal(400, rig.Post(Path1, """{"force":true}""").Status);
        Assert.Equal(Corpus, System.IO.File.ReadAllText(rig.File));
    }

    [Fact]
    public void A_locked_kit_refuses_an_unauthenticated_removal_like_every_other_write()
    {
        using var rig = new Rig(Corpus, password: "secret");
        Assert.Equal(401, rig.Post(Path1).Status);
        Assert.Equal(Corpus, System.IO.File.ReadAllText(rig.File));
    }

    [Fact]
    public void A_routable_bind_without_a_password_refuses_the_removal()
    {
        using var rig = new Rig(Corpus, host: "0.0.0.0");
        Assert.Equal(403, rig.Post(Path1).Status);
        Assert.Equal(Corpus, System.IO.File.ReadAllText(rig.File));
    }

    /// <summary>Removals are read-modify-write on one file: without the edit lock a parallel one resurrects another's block.</summary>
    [Fact]
    public async Task Removals_made_at_the_same_moment_all_land()
    {
        const int n = 40;
        var text = string.Join("\n", Enumerable.Range(1, n).Select(i => $"behaviour R-{i} \"r{i}\"\n  source defined\n  when opens page:Home\n"));
        using var rig = new Rig(text);
        var answers = await Task.WhenAll(Enumerable.Range(1, n).Select(i => Task.Run(() => rig.Post($"/api/projects/demo/behaviours/R-{i}/remove"))));
        Assert.All(answers, a => Assert.Equal(200, a.Status));
        Assert.Equal(string.Empty, System.IO.File.ReadAllText(rig.File).Trim());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    [InlineData("PUT")]
    public void Only_post_reaches_the_removal(string method)
    {
        using var rig = new Rig(Corpus);
        var r = rig.Router.Route(method, Path1);
        Assert.NotEqual(200, r.Status);
        Assert.Equal(method == "GET" ? 404 : 405, r.Status);
        Assert.Equal(Corpus, System.IO.File.ReadAllText(rig.File));
    }
}
