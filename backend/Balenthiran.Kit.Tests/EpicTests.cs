using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c>epic</c> (BEH-EPIC-2, his kit.notes.md 2026-10-19 17:25Z): its own block, joined by a
/// feature's <c>in EPIC-…</c> line, kept apart from the feature's parents.
/// </summary>
public class EpicTests
{
    private readonly ICorpusParser _parser = new CorpusParser();

    private const string WithEpic =
        "epic EPIC-USERS \"Kit supports other users\"\n" +
        "\n" +
        "feature FEAT-UI \"Kit UI\"\n" +
        "\n" +
        "feature FEAT-SIGNUP \"Sign up\"\n" +
        "  in FEAT-UI\n" +
        "  in EPIC-USERS\n" +
        "\n" +
        "behaviour BEH-A \"a\"\n" +
        "  in FEAT-SIGNUP\n" +
        "  when opens page:Home\n" +
        "\n" +
        "epic EPIC-ONBOARD \"Onboard existing projects\"\n";

    [Fact]
    public void An_epic_is_its_own_block_and_a_feature_joins_it_without_changing_its_parents()
    {
        var c = _parser.ParseCorpus(WithEpic, "t.beh");
        Assert.Equal(["EPIC-USERS", "EPIC-ONBOARD"], c.Epics.Select(e => e.Id));
        Assert.Equal("Kit supports other users", c.Epics[0].Title);
        Assert.Equal("t.beh:1", c.Epics[0].At);

        var signup = c.Features.Single(f => f.Id == "FEAT-SIGNUP");
        Assert.Equal(["FEAT-UI"], signup.In.Select(t => t.Id));
        Assert.Equal(["EPIC-USERS"], signup.Epics.Select(t => t.Id));
        Assert.Empty(c.Features.Single(f => f.Id == "FEAT-UI").Epics);
        Assert.Single(c.Behaviours);
    }

    [Theory]
    [InlineData("epic EPIC-A \"a\"\nepic EPIC-A \"again\"\n", "t.beh:2: epic EPIC-A is already declared at t.beh:1")]
    [InlineData("epic FEAT-A \"a\"\n", "t.beh:1: epic wants EPIC-<ID> \"<title>\", got: epic FEAT-A \"a\"")]
    [InlineData("epic EPIC-A \"a\"\n  stage logic\n", "t.beh:2: an epic takes no lines, got: stage logic")]
    [InlineData("feature FEAT-A \"a\"\n  in EPIC-NOPE\n", "t.beh:2: in names EPIC-NOPE, which is not an epic in this file")]
    [InlineData("epic EPIC-A \"a\"\nfeature FEAT-A \"a\"\n  in EPIC-A\n  in EPIC-A\n", "t.beh:4: already in EPIC-A at t.beh:3")]
    [InlineData("epic EPIC-A \"a\"\nbehaviour BEH-A \"a\"\n  in EPIC-A\n", "t.beh:3: only a feature can be in an epic; tag the behaviour's feature with EPIC-A")]
    public void A_malformed_epic_or_epic_tag_is_refused_naming_the_line(string corpus, string message)
    {
        var e = Assert.Throws<CorpusParseException>(() => _parser.Parse(corpus, "t.beh"));
        Assert.Equal(message, e.Message);
    }

    [Fact]
    public void A_corpus_without_epics_serves_the_shape_it_did_before()
    {
        var ser = new EngineJsonSerialiser();
        var plain = JsonNode.Parse(ser.Serialise(new FeatureView { Id = "FEAT-A", Title = "a", In = [], At = "t.beh:1" }))!.AsObject();
        Assert.False(plain.ContainsKey("epics"));
        var tagged = JsonNode.Parse(ser.Serialise(new FeatureView { Id = "FEAT-A", Title = "a", In = [], At = "t.beh:1", Epics = ["EPIC-U"] }))!.AsObject();
        Assert.Equal("EPIC-U", tagged["epics"]![0]!.GetValue<string>());
    }

    private static readonly CorpusWriter Writer = new(new CorpusParser());

    [Fact]
    public void A_step_added_to_the_behaviour_above_an_epic_lands_in_the_behaviour()
    {
        var r = Writer.AddStep(WithEpic, "BEH-A", "then sees heading:Title");
        Assert.True(r.Ok, r.Reason);
        Assert.Contains("  when opens page:Home\n  then sees heading:Title\n\nepic EPIC-ONBOARD", r.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Removing_the_behaviour_above_an_epic_leaves_the_epic()
    {
        var r = Writer.RemoveBehaviour(WithEpic, "BEH-A");
        Assert.True(r.Ok, r.Reason);
        Assert.Equal(2, new CorpusParser().ParseCorpus(r.Text!, "t.beh").Epics.Count);
    }
}
