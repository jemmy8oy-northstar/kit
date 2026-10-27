using System.Text.Json;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The <c>on-dev</c> line (BEH-BOARD-22). Fixtures only: no committed corpus may carry one
/// until the parser that reads it is live, or production would refuse the whole file.
/// </summary>
public class OnDevTests
{
    private const string Corpus = "behaviour BEH-A \"a\"\n  source defined x\n  review approved\n  on-dev \"tracks#24\"\n  then sees field:F\n\nbehaviour BEH-B \"b\"\n  then sees field:F\n";

    private readonly ICorpusParser _parser = new CorpusParser();

    private readonly CorpusWriter _writer = new(new CorpusParser());

    [Fact]
    public void An_on_dev_line_parses_and_marks_only_its_own_behaviour()
    {
        var parsed = _parser.Parse(Corpus, "t.beh");

        Assert.Equal("tracks#24", parsed[0].OnDev);
        Assert.Null(parsed[1].OnDev);
    }

    [Fact]
    public void On_dev_is_absent_from_the_json_until_set_so_no_golden_changes()
    {
        var serialiser = new EngineJsonSerialiser();
        var plain = serialiser.Serialise(_parser.Parse("behaviour BEH-A \"a\"\n  then sees field:F\n", "t.beh"));
        var set = serialiser.Serialise(_parser.Parse(Corpus, "t.beh"));

        Assert.DoesNotContain("onDev", plain, StringComparison.Ordinal);
        Assert.Equal("tracks#24", JsonDocument.Parse(set).RootElement[0].GetProperty("onDev").GetString());
    }

    [Fact]
    public void A_second_on_dev_line_is_refused_like_a_second_needs_context()
    {
        var ex = Assert.Throws<CorpusParseException>(() => _parser.Parse("behaviour BEH-A \"a\"\n  on-dev \"one\"\n  on-dev \"two\"\n", "t.beh"));

        Assert.Equal("t.beh:3: on-dev is already set on BEH-A", ex.Message);
    }

    [Theory]
    [InlineData("on-dev")]
    [InlineData("on-dev \"\"")]
    [InlineData("on-dev \"  \"")]
    [InlineData("on-dev unquoted")]
    public void On_dev_wants_a_quoted_non_empty_line(string line)
    {
        var ex = Assert.Throws<CorpusParseException>(() => _parser.Parse($"behaviour BEH-A \"a\"\n  {line}\n", "t.beh"));

        Assert.StartsWith("t.beh:2: on-dev wants a quoted, non-empty line", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pausing_goes_under_the_on_dev_line_and_resuming_restores_the_text()
    {
        var paused = _writer.SetPaused(Corpus, "BEH-A", true);

        Assert.True(paused.Ok);
        Assert.Equal(Corpus.Replace("  on-dev \"tracks#24\"\n", "  on-dev \"tracks#24\"\n  paused\n", StringComparison.Ordinal), paused.Text);
        Assert.Equal(Corpus, _writer.SetPaused(paused.Text!, "BEH-A", false).Text);
    }

    [Fact]
    public void Adjudicating_a_behaviour_keeps_its_on_dev_line()
    {
        var r = _writer.SetReview(Corpus, "BEH-A", "denied", "no");

        Assert.True(r.Ok);
        Assert.Equal("tracks#24", _parser.Parse(r.Text!, "t.beh")[0].OnDev);
    }
}
