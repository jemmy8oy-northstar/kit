using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The <c>pending</c> marker (kit#155). No committed corpus carries one yet, so
/// the goldens only prove the <c>false</c> half; these are the <c>true</c> half
/// and the refusal.
/// </summary>
public class PendingTests
{
    private readonly ICorpusParser _parser = new CorpusParser();

    private readonly EngineJsonSerialiser _serialiser = new();

    /// <summary>
    /// The <c>true</c> half scored against NODE, not against an expectation written here:
    /// <c>Fixtures/pending-edges.json</c> is <c>conformance.js</c>'s output for the corpus beside
    /// it (its header says how to regenerate). A blind review of kit#158 found that no golden held
    /// <c>"pending": true</c>, so the two parsers had never been compared on it.
    /// </summary>
    [Fact]
    public void The_parse_of_pending_behaviours_reproduces_Node()
    {
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Fixtures, "pending-edges.json")))!["parse"]!;

        // The fixture is only worth having while it still holds what it was built for.
        Assert.Equal(4, golden.AsArray().Count(b => b!["pending"]!.GetValue<bool>()));
        Assert.Equal(1, golden.AsArray().Count(b => !b!["pending"]!.GetValue<bool>()));

        var actual = _parser.Parse(File.ReadAllText(Path.Combine(RepoLayout.Fixtures, "pending-edges.beh")), "pending-edges.beh");

        // Order-preserving on both sides: a `pending` written at a different key position fails here.
        Assert.Equal(
            JsonSerializer.Serialize(golden, _serialiser.Options),
            JsonSerializer.Serialize(JsonNode.Parse(_serialiser.Serialise(actual)), _serialiser.Options));
    }

    [Fact]
    public void A_pending_line_marks_only_its_own_behaviour()
    {
        var parsed = _parser.Parse("behaviour BEH-A \"a\"\n  pending\n  then sees field:F\nbehaviour BEH-B \"b\"\n  then sees field:F\n", "t.beh");

        Assert.True(parsed[0].Pending);
        Assert.False(parsed[1].Pending);
    }

    [Fact]
    public void Pending_takes_nothing_after_it()
    {
        var ex = Assert.Throws<CorpusParseException>(() => _parser.Parse("behaviour BEH-A \"a\"\n  pending later\n", "t.beh"));

        Assert.Equal("t.beh:2: pending takes nothing after it, got: later", ex.Message);
    }
}
