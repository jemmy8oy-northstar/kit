using System.Text.Json;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>BEH-INBOX-1: lowering a card's priority is an ordinary edit of its `priority` line.</summary>
public class SetPriorityTests
{
    private const string Corpus = "behaviour BEH-A \"a\"\n  source defined x\n  review approved\n  priority 2\n  then sees field:F\n\nbehaviour BEH-B \"b\"\n  then sees field:F\n";

    private readonly CorpusParser _parser = new();

    private readonly CorpusWriter _writer = new(new CorpusParser());

    [Fact]
    public void An_existing_priority_line_is_replaced_in_place_and_nothing_else_moves()
    {
        var r = _writer.SetPriority(Corpus, "BEH-A", 3);

        Assert.True(r.Ok);
        Assert.Equal(Corpus.Replace("priority 2", "priority 3", StringComparison.Ordinal), r.Text);
        Assert.Equal(3, _parser.Parse(r.Text!, "t.beh")[0].Priority);
    }

    [Fact]
    public void A_card_with_no_priority_gets_one_line_under_its_head_and_its_neighbour_is_untouched()
    {
        var r = _writer.SetPriority(Corpus, "BEH-B", 5);

        Assert.True(r.Ok);
        var parsed = _parser.Parse(r.Text!, "t.beh");
        Assert.Equal(5, parsed[1].Priority);
        Assert.Equal(2, parsed[0].Priority);
        Assert.Equal(Corpus.Replace("behaviour BEH-B \"b\"\n", "behaviour BEH-B \"b\"\n  priority 5\n", StringComparison.Ordinal), r.Text);
    }

    [Fact]
    public void Setting_what_is_already_set_changes_nothing()
    {
        Assert.Equal(Corpus, _writer.SetPriority(Corpus, "BEH-A", 2).Text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void A_score_outside_1_to_5_is_refused(int score)
    {
        var r = _writer.SetPriority(Corpus, "BEH-A", score);

        Assert.False(r.Ok);
        Assert.Equal("bad-priority", r.Error);
    }

    [Fact]
    public void An_unknown_behaviour_is_refused_naming_the_known_ones()
    {
        var r = _writer.SetPriority(Corpus, "BEH-NOPE", 3);

        Assert.False(r.Ok);
        Assert.Equal("no-such-behaviour", r.Error);
        Assert.Equal(["BEH-A", "BEH-B"], r.Known);
    }

    private sealed class Rig : IDisposable
    {
        private readonly string root = Directory.CreateTempSubdirectory("kit-priority-").FullName;
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

    private const string Path1 = "/api/projects/demo/behaviours/BEH-A/priority";

    [Fact]
    public void The_priority_route_writes_the_writers_text_to_disk()
    {
        using var rig = new Rig(Corpus);

        Assert.Equal(200, rig.Post(Path1, """{"priority":3}""").Status);
        Assert.Equal(_writer.SetPriority(Corpus, "BEH-A", 3).Text, System.IO.File.ReadAllText(rig.File));
    }

    [Theory]
    [InlineData("""{"priority":0}""", 400)]
    [InlineData("""{"priority":6}""", 400)]
    [InlineData("""{"priority":2.5}""", 400)]
    [InlineData("""{"priority":"3"}""", 400)]
    [InlineData("""{}""", 400)]
    public void A_bad_priority_is_a_400_and_leaves_the_file_byte_identical(string body, int status)
    {
        using var rig = new Rig(Corpus);

        Assert.Equal(status, rig.Post(Path1, body).Status);
        Assert.Equal(Corpus, System.IO.File.ReadAllText(rig.File));
    }

    [Fact]
    public void An_unknown_behaviour_or_project_or_an_indexed_path_is_refused()
    {
        using var rig = new Rig(Corpus);

        Assert.Equal(409, rig.Post("/api/projects/demo/behaviours/NOPE-1/priority", """{"priority":3}""").Status);
        Assert.Equal(404, rig.Post("/api/projects/ghost/behaviours/BEH-A/priority", """{"priority":3}""").Status);
        Assert.Equal(404, rig.Post(Path1 + "/0", """{"priority":3}""").Status);
        Assert.Equal(Corpus, System.IO.File.ReadAllText(rig.File));
    }

    [Fact]
    public void A_locked_kit_refuses_an_unauthenticated_priority_edit()
    {
        using var rig = new Rig(Corpus, password: "secret");

        Assert.Equal(401, rig.Post(Path1, """{"priority":3}""").Status);
        Assert.Equal(Corpus, System.IO.File.ReadAllText(rig.File));
    }
}
