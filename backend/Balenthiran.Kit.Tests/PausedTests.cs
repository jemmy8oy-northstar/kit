using System.Text.Json;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The <c>paused</c> line (BEH-BOARD-16). Fixtures only: no committed corpus may carry one until
/// the parser that reads it is live, or production would refuse the whole file.
/// </summary>
public class PausedTests
{
    private const string Corpus = "behaviour BEH-A \"a\"\n  source defined x\n  review approved\n  priority 2\n  then sees field:F\n\nbehaviour BEH-B \"b\"\n  then sees field:F\n";

    private readonly ICorpusParser _parser = new CorpusParser();

    private readonly CorpusWriter _writer = new(new CorpusParser());

    [Fact]
    public void A_paused_line_marks_only_its_own_behaviour()
    {
        var parsed = _parser.Parse("behaviour BEH-A \"a\"\n  paused\n  then sees field:F\nbehaviour BEH-B \"b\"\n  then sees field:F\n", "t.beh");

        Assert.True(parsed[0].Paused);
        Assert.False(parsed[1].Paused);
    }

    [Fact]
    public void Paused_takes_nothing_after_it()
    {
        var ex = Assert.Throws<CorpusParseException>(() => _parser.Parse("behaviour BEH-A \"a\"\n  paused later\n", "t.beh"));

        Assert.Equal("t.beh:2: paused takes nothing after it, got: later", ex.Message);
    }

    [Fact]
    public void A_second_paused_line_is_refused_like_a_second_priority()
    {
        var ex = Assert.Throws<CorpusParseException>(() => _parser.Parse("behaviour BEH-A \"a\"\n  paused\n  paused\n", "t.beh"));

        Assert.Equal("t.beh:3: paused is already set on BEH-A", ex.Message);
    }

    [Fact]
    public void Paused_is_absent_from_the_json_until_set_so_no_golden_changes()
    {
        var serialiser = new EngineJsonSerialiser();
        var plain = serialiser.Serialise(_parser.Parse("behaviour BEH-A \"a\"\n  then sees field:F\n", "t.beh"));
        var paused = serialiser.Serialise(_parser.Parse("behaviour BEH-A \"a\"\n  paused\n  then sees field:F\n", "t.beh"));

        Assert.DoesNotContain("paused", plain, StringComparison.Ordinal);
        Assert.True(JsonDocument.Parse(paused).RootElement[0].GetProperty("paused").GetBoolean());
    }

    [Fact]
    public void Pausing_adds_one_line_under_the_head_of_that_behaviour_only()
    {
        var r = _writer.SetPaused(Corpus, "BEH-A", true);

        Assert.True(r.Ok);
        Assert.Equal(Corpus.Replace("  priority 2\n", "  priority 2\n  paused\n", StringComparison.Ordinal), r.Text);
        var parsed = _parser.Parse(r.Text!, "t.beh");
        Assert.True(parsed[0].Paused);
        Assert.False(parsed[1].Paused);
    }

    [Fact]
    public void Resuming_removes_the_line_and_restores_the_text_exactly()
    {
        var paused = _writer.SetPaused(Corpus, "BEH-A", true).Text!;
        var resumed = _writer.SetPaused(paused, "BEH-A", false);

        Assert.True(resumed.Ok);
        Assert.Equal(Corpus, resumed.Text);
    }

    [Fact]
    public void Pausing_what_is_paused_or_resuming_what_is_not_changes_nothing()
    {
        var paused = _writer.SetPaused(Corpus, "BEH-B", true).Text!;

        Assert.Equal(paused, _writer.SetPaused(paused, "BEH-B", true).Text);
        Assert.Equal(Corpus, _writer.SetPaused(Corpus, "BEH-B", false).Text);
    }

    [Fact]
    public void Pausing_an_unknown_behaviour_is_refused_naming_the_known_ones()
    {
        var r = _writer.SetPaused(Corpus, "BEH-NOPE", true);

        Assert.False(r.Ok);
        Assert.Equal("no-such-behaviour", r.Error);
        Assert.Equal(["BEH-A", "BEH-B"], r.Known);
    }
}
