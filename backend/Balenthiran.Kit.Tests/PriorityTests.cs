using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// A <c>priority</c> line (kit#284): 1 is first, 5 is last. No committed corpus has one,
/// so the goldens only prove its absence; these are the values and the refusals.
/// </summary>
public class PriorityTests
{
    private readonly ICorpusParser _parser = new CorpusParser();

    private readonly EngineJsonSerialiser _serialiser = new();

    [Fact]
    public void A_priority_line_is_read_as_its_number()
    {
        var parsed = _parser.Parse("behaviour BEH-A \"a\"\n  source inferred kit#284\n  priority 2\n", "t.beh");

        Assert.Equal(2, parsed[0].Priority);
    }

    [Fact]
    public void Without_a_line_there_is_no_priority_and_none_is_written()
    {
        var parsed = _parser.Parse("behaviour BEH-A \"a\"\n  then sees field:F\n", "t.beh");

        Assert.Null(parsed[0].Priority);
        Assert.False(JsonNode.Parse(_serialiser.Serialise(parsed))![0]!.AsObject().ContainsKey("priority"));
    }

    [Theory]
    [InlineData("behaviour BEH-A \"a\"\n  priority 0\n", "t.beh:2: priority wants 1 (first) to 5, got: 0")]
    [InlineData("behaviour BEH-A \"a\"\n  priority 6\n", "t.beh:2: priority wants 1 (first) to 5, got: 6")]
    [InlineData("behaviour BEH-A \"a\"\n  priority 10\n", "t.beh:2: priority wants 1 (first) to 5, got: 10")]
    [InlineData("behaviour BEH-A \"a\"\n  priority high\n", "t.beh:2: priority wants 1 (first) to 5, got: high")]
    [InlineData("behaviour BEH-A \"a\"\n  priority\n", "t.beh:2: priority wants 1 (first) to 5, got: ")]
    [InlineData("behaviour BEH-A \"a\"\n  priority 1\n  priority 3\n", "t.beh:3: priority is already set on BEH-A")]
    public void A_priority_Kit_cannot_rank_is_refused(string corpus, string message)
    {
        var ex = Assert.Throws<CorpusParseException>(() => _parser.Parse(corpus, "t.beh"));

        Assert.Equal(message, ex.Message);
    }
}
