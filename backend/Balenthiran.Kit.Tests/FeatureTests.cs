using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c>feature</c> and <c>in</c> (kit#237, docs/design/features.md): BEH-PARSE-2's three refusals,
/// the wire the projects view reads, and the writer leaving a feature block alone.
/// </summary>
public class FeatureTests
{
    private readonly ICorpusParser _parser = new CorpusParser();

    private const string Tagged =
        "feature FEAT-UI \"Kit UI\"\n" +
        "\n" +
        "feature FEAT-BOARD \"Board\"\n" +
        "  in FEAT-UI\n" +
        "\n" +
        "behaviour BEH-A \"a\"\n" +
        "  in FEAT-BOARD\n" +
        "  in FEAT-UI\n" +
        "  when opens page:Home\n" +
        "\n" +
        "feature FEAT-CARD \"Card\"\n" +
        "  # a component on two pages\n" +
        "  in FEAT-BOARD\n" +
        "  in FEAT-UI\n" +
        "\n" +
        "behaviour BEH-B \"b\"\n" +
        "  when opens page:Home\n";

    [Fact]
    public void Features_and_their_parents_are_read_in_order_and_a_tag_can_repeat_across_parents()
    {
        var c = _parser.ParseCorpus(Tagged, "t.beh");

        Assert.Equal(["FEAT-UI", "FEAT-BOARD", "FEAT-CARD"], c.Features.Select(f => f.Id));
        Assert.Equal("Board", c.Features[1].Title);
        Assert.Equal("t.beh:3", c.Features[1].At);
        Assert.Empty(c.Features[0].In);
        Assert.Equal(["FEAT-BOARD", "FEAT-UI"], c.Features[2].In.Select(t => t.Id));

        Assert.Equal(["BEH-A", "BEH-B"], c.Behaviours.Select(b => b.Id));
        Assert.Equal(["FEAT-BOARD", "FEAT-UI"], c.Behaviours[0].In.Select(t => t.Id));
        Assert.Equal("t.beh:7", c.Behaviours[0].In[0].At);
        Assert.Empty(c.Behaviours[1].In);
    }

    [Fact]
    public void Parse_and_ParseCorpus_return_the_same_behaviours()
    {
        Assert.Equal(
            _parser.ParseCorpus(Tagged, "t.beh").Behaviours.Select(b => b.Id),
            _parser.Parse(Tagged, "t.beh").Select(b => b.Id));
    }

    /// <summary>
    /// Why the engine's <c>in</c> is absent rather than <c>[]</c>: every frozen golden predates it.
    /// An untagged behaviour must serialise exactly as it did, and a tagged one after <c>layer</c>.
    /// </summary>
    [Fact]
    public void The_engine_writes_in_only_when_there_is_one_and_after_layer()
    {
        var json = JsonNode.Parse(new EngineJsonSerialiser().Serialise(_parser.Parse(Tagged, "t.beh")))!.AsArray();

        var keys = json[0]!.AsObject().Select(kv => kv.Key).ToList();
        Assert.Equal(keys.IndexOf("layer") + 1, keys.IndexOf("in"));
        Assert.Equal("FEAT-BOARD", json[0]!["in"]![0]!["id"]!.GetValue<string>());
        Assert.False(json[1]!.AsObject().ContainsKey("in"));
    }

    [Theory]
    [InlineData("behaviour BEH-A \"a\"\n  in FEAT-NOPE\n", "t.beh:2: in names FEAT-NOPE, which is not a feature in this file")]
    [InlineData("feature FEAT-A \"a\"\n  in FEAT-NOPE\n", "t.beh:2: in names FEAT-NOPE, which is not a feature in this file")]
    [InlineData("behaviour BEH-A \"a\"\n  in FEAT-NOPE\nfeature FEAT-A \"a\"\n  in FEAT-GONE\n", "t.beh:2: in names FEAT-NOPE, which is not a feature in this file")]
    [InlineData("feature FEAT-A \"a\"\n  in FEAT-A\n", "t.beh:2: FEAT-A is its own ancestor: FEAT-A → FEAT-A")]
    [InlineData("feature FEAT-A \"a\"\n  in FEAT-B\nfeature FEAT-B \"b\"\n  in FEAT-C\nfeature FEAT-C \"c\"\n  in FEAT-A\n", "t.beh:6: FEAT-A is its own ancestor: FEAT-A → FEAT-B → FEAT-C → FEAT-A")]
    [InlineData("feature FEAT-A \"a\"\n\nfeature FEAT-A \"again\"\n", "t.beh:3: feature FEAT-A is already declared at t.beh:1")]
    public void A_tag_graph_that_points_nowhere_loops_or_repeats_an_id_is_refused_naming_the_line(string corpus, string message)
    {
        var e = Assert.Throws<CorpusParseException>(() => _parser.Parse(corpus, "t.beh"));
        Assert.Equal(message, e.Message);
    }

    [Theory]
    [InlineData("behaviour BEH-A \"a\"\n  in BEH-B\nbehaviour BEH-B \"b\"\n", "t.beh:2: in wants a feature id (FEAT-…), got: BEH-B")]
    [InlineData("feature BOARD \"a\"\n", "t.beh:1: feature wants FEAT-<ID> \"<title>\", got: feature BOARD \"a\"")]
    [InlineData("feature FEAT-A \"a\"\n  when opens page:Home\n", "t.beh:2: a feature takes only \"in\" lines, got: when opens page:Home")]
    [InlineData("feature FEAT-A \"a\"\nbehaviour BEH-A \"a\"\n  in FEAT-A\n  in FEAT-A\n", "t.beh:4: already in FEAT-A at t.beh:3")]
    [InlineData("in FEAT-A\n", "t.beh:1: line outside a behaviour: in FEAT-A")]
    public void A_malformed_feature_or_tag_line_is_refused(string corpus, string message)
    {
        var e = Assert.Throws<CorpusParseException>(() => _parser.Parse(corpus, "t.beh"));
        Assert.Equal(message, e.Message);
    }

    [Fact]
    public void Two_parents_sharing_an_ancestor_is_a_diamond_not_a_loop()
    {
        var c = _parser.ParseCorpus(
            "feature FEAT-TOP \"top\"\nfeature FEAT-L \"l\"\n  in FEAT-TOP\nfeature FEAT-R \"r\"\n  in FEAT-TOP\n" +
            "feature FEAT-CARD \"card\"\n  in FEAT-L\n  in FEAT-R\nbehaviour BEH-A \"a\"\n  in FEAT-CARD\n", "t.beh");

        Assert.Equal(4, c.Features.Count);
    }

    [Fact]
    public void A_dangling_tag_also_stops_the_behaviour_list_so_check_and_report_refuse_it_too()
    {
        Assert.Throws<CorpusParseException>(() => _parser.Parse("behaviour BEH-A \"a\"\n  in FEAT-NOPE\n", "t.beh"));
    }

    // ── The writer ─────────────────────────────────────────────────────────

    private static readonly CorpusWriter Writer = new(new CorpusParser());

    [Fact]
    public void A_step_added_to_the_behaviour_above_a_feature_lands_in_the_behaviour()
    {
        var r = Writer.AddStep(Tagged, "BEH-A", "then sees heading:Title");
        Assert.True(r.Ok, r.Reason);
        Assert.Contains("  when opens page:Home\n  then sees heading:Title\n\nfeature FEAT-CARD", r.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Removing_the_behaviour_above_a_feature_leaves_the_feature()
    {
        var r = Writer.RemoveBehaviour(Tagged, "BEH-A");
        Assert.True(r.Ok, r.Reason);
        Assert.Contains("feature FEAT-CARD \"Card\"\n  # a component on two pages\n  in FEAT-BOARD\n  in FEAT-UI\n", r.Text, StringComparison.Ordinal);
        Assert.Equal(3, new CorpusParser().ParseCorpus(r.Text!, "t.beh").Features.Count);
    }

    [Fact]
    public void An_edit_keeps_the_behaviours_own_tags()
    {
        var r = Writer.Retitle(Tagged, "BEH-A", "renamed");
        Assert.True(r.Ok, r.Reason);
        Assert.Equal(["FEAT-BOARD", "FEAT-UI"], new CorpusParser().Parse(r.Text!, "t.beh")[0].In.Select(t => t.Id));
    }

    // ── The wire ───────────────────────────────────────────────────────────

    [Fact]
    public void The_project_view_serves_features_and_each_behaviours_tags_with_untagged_as_empty()
    {
        var dir = Directory.CreateTempSubdirectory("kit-features-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "demo.beh"), Tagged);
            var corpora = new CorpusDirectory(dir.FullName, dir.FullName);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());

            var view = (ProjectView)viewer.View("demo");

            Assert.Equal(["FEAT-UI", "FEAT-BOARD", "FEAT-CARD"], view.Features.Select(f => f.Id));
            Assert.Equal(["FEAT-BOARD", "FEAT-UI"], view.Features[2].In);
            Assert.Equal(["FEAT-BOARD", "FEAT-UI"], view.Behaviours[0].In);
            Assert.Empty(view.Behaviours[1].In);

            var json = JsonNode.Parse(new EngineJsonSerialiser().Serialise(view))!;
            Assert.Equal("features", json.AsObject().Last().Key);
            Assert.Equal("in", json["behaviours"]![1]!.AsObject().Last().Key);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
