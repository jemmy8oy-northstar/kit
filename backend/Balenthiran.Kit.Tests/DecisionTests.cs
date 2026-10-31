using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c>decision</c> (FEAT-ARCH, BEH-ARCH-1 slice A): its own block, owned by one part of the system,
/// read-only. A behaviour edit beside it must leave it exactly as it was.
/// </summary>
public class DecisionTests
{
    private readonly ICorpusParser _parser = new CorpusParser();

    private const string WithDecision =
        "behaviour BEH-A \"a\"\n" +
        "  when opens page:Home\n" +
        "\n" +
        "decision ADR-BACKEND-1 \"The backend is ASP.NET Core\"\n" +
        "  part backend\n" +
        "  chosen ASP.NET Core (Kestrel)\n" +
        "  considered Node/Express\n" +
        "  considered Go\n" +
        "  because one typed codebase\n" +
        "  because it ports the engine\n" +
        "  source defined docs/x.md#y\n" +
        "  review unreviewed\n" +
        "\n" +
        "decision ADR-DATA-1 \"Data is files\"\n" +
        "  part data\n" +
        "  chosen corpus files in git\n" +
        "  because humans can edit them\n";

    [Fact]
    public void A_decision_is_its_own_block_with_a_part_and_repeating_considered_and_because()
    {
        var c = _parser.ParseCorpus(WithDecision, "t.beh");
        Assert.Equal(["ADR-BACKEND-1", "ADR-DATA-1"], c.Decisions.Select(d => d.Id));
        var d = c.Decisions[0];
        Assert.Equal("The backend is ASP.NET Core", d.Title);
        Assert.Equal("backend", d.Part);
        Assert.Equal("ASP.NET Core (Kestrel)", d.Chosen);
        Assert.Equal(["Node/Express", "Go"], d.Considered);
        Assert.Equal(["one typed codebase", "it ports the engine"], d.Because);
        Assert.Equal("defined", d.Source.Origin);
        Assert.Equal("docs/x.md#y", d.Source.Ref);
        Assert.Equal("unreviewed", d.Review.State);
        Assert.Equal("t.beh:4", d.At);
        Assert.Empty(c.Decisions[1].Considered);
        Assert.Equal("unreviewed", c.Decisions[1].Review.State);
        Assert.Single(c.Behaviours);
    }

    [Theory]
    [InlineData("decision ADR-A \"a\"\n  part kitchen\n", "t.beh:2: part wants \"frontend\"|\"backend\"|\"data\"|\"deployment\", got: kitchen")]
    [InlineData("decision FEAT-A \"a\"\n", "t.beh:1: decision wants ADR-<ID> \"<title>\", got: decision FEAT-A \"a\"")]
    [InlineData("decision ADR-A \"a\"\ndecision ADR-A \"b\"\n", "t.beh:2: decision ADR-A is already declared at t.beh:1")]
    [InlineData("decision ADR-A \"a\"\n  part data\n  stage logic\n", "t.beh:3: unrecognised keyword \"stage\" in a decision, got: stage logic")]
    [InlineData("decision ADR-A \"a\"\n  chosen x\n  because y\n", "t.beh:1: decision ADR-A needs a part (frontend|backend|data|deployment)")]
    [InlineData("decision ADR-A \"a\"\n  part data\n  because y\n", "t.beh:1: decision ADR-A needs a chosen line")]
    [InlineData("decision ADR-A \"a\"\n  part data\n  chosen x\n", "t.beh:1: decision ADR-A needs at least one because line")]
    [InlineData("decision ADR-A \"a\"\n  part data\n  part data\n", "t.beh:3: part is already set on ADR-A")]
    public void A_malformed_decision_is_refused_naming_the_line(string corpus, string message)
    {
        var e = Assert.Throws<CorpusParseException>(() => _parser.Parse(corpus, "t.beh"));
        Assert.Equal(message, e.Message);
    }

    [Fact]
    public void A_decision_is_served_with_its_part_review_and_empty_lists()
    {
        var ser = new EngineJsonSerialiser();
        var view = new DecisionView
        {
            Id = "ADR-A", Title = "t", Part = "data", Chosen = "c", Considered = [], Because = ["b"],
            Review = new Review { State = "unreviewed", Note = null }, Source = new Source { Origin = "defined", Ref = null }, At = "t.beh:1",
        };
        var node = JsonNode.Parse(ser.Serialise(view))!.AsObject();
        Assert.Equal("ADR-A", node["id"]!.GetValue<string>());
        Assert.Equal("unreviewed", node["review"]!["state"]!.GetValue<string>());
        Assert.Empty(node["considered"]!.AsArray());
    }

    private static readonly CorpusWriter Writer = new(new CorpusParser());

    [Fact]
    public void A_step_added_to_the_behaviour_above_a_decision_leaves_the_decision_intact()
    {
        var r = Writer.AddStep(WithDecision, "BEH-A", "then sees heading:Title");
        Assert.True(r.Ok, r.Reason);
        Assert.Contains("  when opens page:Home\n  then sees heading:Title\n\ndecision ADR-BACKEND-1", r.Text, StringComparison.Ordinal);
        Assert.Equal(
            new CorpusParser().ParseCorpus(WithDecision, "t.beh").Decisions.Select(d => (d.Id, d.Chosen, d.Because.Count)),
            new CorpusParser().ParseCorpus(r.Text!, "t.beh").Decisions.Select(d => (d.Id, d.Chosen, d.Because.Count)));
        Assert.EndsWith("  because humans can edit them\n", r.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Removing_the_behaviour_above_a_decision_leaves_both_decisions()
    {
        var r = Writer.RemoveBehaviour(WithDecision, "BEH-A");
        Assert.True(r.Ok, r.Reason);
        Assert.Equal(2, new CorpusParser().ParseCorpus(r.Text!, "t.beh").Decisions.Count);
    }

    // ── BEH-ARCH-1 slice B1: approve or deny a decision ───────────────────────────────────

    [Fact]
    public void BEH_ARCH_1_approving_a_decision_rewrites_only_its_own_review_line()
    {
        var r = Writer.SetDecisionReview(WithDecision, "ADR-BACKEND-1", "approved");

        Assert.True(r.Ok, r.Reason);
        Assert.Equal(WithDecision.Replace("  review unreviewed\n", "  review approved\n", StringComparison.Ordinal), r.Text);
        var d = new CorpusParser().ParseCorpus(r.Text!, "t.beh").Decisions;
        Assert.Equal("approved", d[0].Review.State);
        Assert.Equal("unreviewed", d[1].Review.State);
    }

    [Fact]
    public void BEH_ARCH_1_a_decision_with_no_review_line_gets_one_at_the_end_of_its_own_block_not_the_next()
    {
        var r = Writer.SetDecisionReview(WithDecision, "ADR-DATA-1", "denied", "it is a database");

        Assert.True(r.Ok, r.Reason);
        Assert.Equal(WithDecision + "  review denied it is a database\n", r.Text);
        Assert.Equal("it is a database", new CorpusParser().ParseCorpus(r.Text!, "t.beh").Decisions[1].Review.Note);
    }

    [Fact]
    public void BEH_ARCH_1_a_same_named_review_in_a_behaviour_above_is_never_touched()
    {
        var corpus = "behaviour BEH-A \"a\"\n  source defined x\n  review unreviewed\n  when opens page:Home\n\n" + WithDecision.Split("\n\n", 2)[1];
        var r = Writer.SetDecisionReview(corpus, "ADR-BACKEND-1", "approved");

        Assert.True(r.Ok, r.Reason);
        Assert.StartsWith("behaviour BEH-A \"a\"\n  source defined x\n  review unreviewed\n  when opens page:Home\n\ndecision", r.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void BEH_ARCH_1_denying_a_decision_without_a_correction_is_refused_and_writes_nothing()
    {
        var r = Writer.SetDecisionReview(WithDecision, "ADR-BACKEND-1", "denied");

        Assert.False(r.Ok);
        Assert.Equal("would-not-parse", r.Error);
        Assert.Contains("a denied decision must state the correction", r.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("approved\n  chosen something else")]
    [InlineData("maybe")]
    [InlineData("")]
    public void BEH_ARCH_1_a_bad_state_is_refused(string state)
    {
        Assert.False(Writer.SetDecisionReview(WithDecision, "ADR-BACKEND-1", state).Ok);
    }

    [Fact]
    public void BEH_ARCH_1_a_note_cannot_smuggle_a_second_line()
    {
        Assert.Equal("multiline-review", Writer.SetDecisionReview(WithDecision, "ADR-BACKEND-1", "denied", "no\n  chosen x").Error);
    }

    [Fact]
    public void BEH_ARCH_1_an_unknown_decision_is_refused_naming_the_known_ones()
    {
        var r = Writer.SetDecisionReview(WithDecision, "ADR-NOPE-1", "approved");

        Assert.Equal("no-such-decision", r.Error);
        Assert.Equal(["ADR-BACKEND-1", "ADR-DATA-1"], r.Known);
    }

    [Fact]
    public void BEH_ARCH_1_the_decision_route_adjudicates_through_the_same_write_as_a_behaviour()
    {
        var root = Directory.CreateTempSubdirectory("kit-decision-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "demo.beh"), WithDecision);
            var corpora = new Balenthiran.Kit.Database.CorpusDirectory(root, root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            var router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), null);

            var bad = router.Route("POST", "/api/projects/demo/decisions/ADR-DATA-1/review", body: JsonDocument.Parse("""{"state":"denied","note":null}""").RootElement);
            Assert.Equal(409, bad.Status);
            Assert.Equal(WithDecision, File.ReadAllText(Path.Combine(root, "demo.beh")));

            var wrongType = router.Route("POST", "/api/projects/demo/decisions/ADR-DATA-1/review", body: JsonDocument.Parse("""{"state":3}""").RootElement);
            Assert.Equal(400, wrongType.Status);

            var missing = router.Route("POST", "/api/projects/demo/decisions/ADR-NOPE-1/review", body: JsonDocument.Parse("""{"state":"approved"}""").RootElement);
            Assert.Equal(409, missing.Status);

            var ok = router.Route("POST", "/api/projects/demo/decisions/ADR-DATA-1/review", body: JsonDocument.Parse("""{"state":"denied","note":"files do not scale"}""").RootElement);
            Assert.Equal(200, ok.Status);
            var after = new CorpusParser().ParseCorpus(File.ReadAllText(Path.Combine(root, "demo.beh")), "t.beh").Decisions;
            Assert.Equal("denied", after[1].Review.State);
            Assert.Equal("files do not scale", after[1].Review.Note);
            Assert.Equal("unreviewed", after[0].Review.State);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
