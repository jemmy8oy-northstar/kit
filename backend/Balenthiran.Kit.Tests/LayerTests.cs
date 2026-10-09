using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// Behaviour layers (kit#89): a <c>layer</c> line and a <c># kit:layer</c> file
/// directive. Every committed corpus is <c>ux</c>, so the goldens only prove the
/// default; these are the other values and the refusals.
/// </summary>
public class LayerTests
{
    private readonly ICorpusParser _parser = new CorpusParser();

    private readonly EngineJsonSerialiser _serialiser = new();

    /// <summary>
    /// Scored against NODE: <c>Fixtures/layer-edges.json</c> is <c>conformance.js</c>'s output
    /// for the corpus beside it (its header says how to regenerate).
    /// </summary>
    [Fact]
    public void The_parse_of_layered_behaviours_reproduces_Node()
    {
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Fixtures, "layer-edges.json")))!["parse"]!;

        // The fixture is only worth having while it still holds every value.
        Assert.Equal(
            ["technical", "ux", "ui", "technical", "ux"],
            golden.AsArray().Select(b => b!["layer"]!.GetValue<string>()));

        var actual = _parser.Parse(File.ReadAllText(Path.Combine(RepoLayout.Fixtures, "layer-edges.beh")), "layer-edges.beh");

        Assert.Equal(
            JsonSerializer.Serialize(golden, _serialiser.Options),
            JsonSerializer.Serialize(JsonNode.Parse(_serialiser.Serialise(actual)), _serialiser.Options));
    }

    [Fact]
    public void Without_a_line_or_a_directive_a_behaviour_is_ux()
    {
        var parsed = _parser.Parse("behaviour BEH-A \"a\"\n  then sees field:F\n", "t.beh");

        Assert.Equal("ux", parsed[0].Layer);
    }

    [Theory]
    [InlineData("behaviour BEH-A \"a\"\n  layer visual\n", "t.beh:2: layer wants \"ux\"|\"technical\"|\"ui\", got: visual")]
    [InlineData("behaviour BEH-A \"a\"\n  layer\n", "t.beh:2: layer wants \"ux\"|\"technical\"|\"ui\", got: ")]
    [InlineData("# kit:layer backend\nbehaviour BEH-A \"a\"\n", "t.beh:1: kit:layer wants \"ux\"|\"technical\"|\"ui\", got: backend")]
    [InlineData("# kit:layer\nbehaviour BEH-A \"a\"\n", "t.beh:1: kit:layer wants \"ux\"|\"technical\"|\"ui\", got: ")]
    [InlineData("# kit:layer ux\n#kit:layer technical\nbehaviour BEH-A \"a\"\n", "t.beh:2: kit:layer is already set at t.beh:1")]
    public void A_layer_Kit_does_not_know_is_refused(string corpus, string message)
    {
        var ex = Assert.Throws<CorpusParseException>(() => _parser.Parse(corpus, "t.beh"));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void A_comment_that_only_mentions_the_directive_is_not_one()
    {
        var parsed = _parser.Parse("# kit:layers are described on kit#89\n# see kit:layer\nbehaviour BEH-A \"a\"\n", "t.beh");

        Assert.Equal("ux", parsed[0].Layer);
    }
}
