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
}
