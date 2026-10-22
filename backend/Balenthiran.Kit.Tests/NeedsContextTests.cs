using System.Text.Json;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The <c>needs-context</c> line (BEH-BOARD-17). Fixtures only: no committed corpus may carry one
/// until the parser that reads it is live, or production would refuse the whole file.
/// </summary>
public class NeedsContextTests
{
    private const string Corpus = "behaviour BEH-A \"a\"\n  source defined x\n  review approved\n  priority 2\n  needs-context \"which colour?\"\n  then sees field:F\n\nbehaviour BEH-B \"b\"\n  then sees field:F\n";

    private readonly ICorpusParser _parser = new CorpusParser();

    private readonly CorpusWriter _writer = new(new CorpusParser());

    [Fact]
    public void A_needs_context_line_parses_and_marks_only_its_own_behaviour()
    {
        var parsed = _parser.Parse("behaviour BEH-A \"a\"\n  needs-context \"which colour?\"\n  then sees field:F\nbehaviour BEH-B \"b\"\n  then sees field:F\n", "t.beh");

        Assert.Equal("which colour?", parsed[0].NeedsContext);
        Assert.Null(parsed[1].NeedsContext);
    }

    [Fact]
    public void Needs_context_is_absent_from_the_json_until_set_so_no_golden_changes()
    {
        var serialiser = new EngineJsonSerialiser();
        var plain = serialiser.Serialise(_parser.Parse("behaviour BEH-A \"a\"\n  then sees field:F\n", "t.beh"));
        var set = serialiser.Serialise(_parser.Parse("behaviour BEH-A \"a\"\n  needs-context \"which colour?\"\n  then sees field:F\n", "t.beh"));

        Assert.DoesNotContain("needsContext", plain, StringComparison.Ordinal);
        Assert.Equal("which colour?", JsonDocument.Parse(set).RootElement[0].GetProperty("needsContext").GetString());
    }

    [Fact]
    public void A_second_needs_context_line_is_refused_like_a_second_paused()
    {
        var ex = Assert.Throws<CorpusParseException>(() => _parser.Parse("behaviour BEH-A \"a\"\n  needs-context \"one\"\n  needs-context \"two\"\n", "t.beh"));

        Assert.Equal("t.beh:3: needs-context is already set on BEH-A", ex.Message);
    }

    [Theory]
    [InlineData("needs-context")]
    [InlineData("needs-context \"\"")]
    [InlineData("needs-context \"  \"")]
    [InlineData("needs-context unquoted")]
    public void Needs_context_wants_a_quoted_non_empty_line(string line)
    {
        var ex = Assert.Throws<CorpusParseException>(() => _parser.Parse($"behaviour BEH-A \"a\"\n  {line}\n", "t.beh"));

        Assert.StartsWith("t.beh:2: needs-context wants a quoted, non-empty line", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pausing_and_resuming_keep_the_needs_context_line()
    {
        var paused = _writer.SetPaused(Corpus, "BEH-A", true);

        Assert.True(paused.Ok);
        // Under the last head line, which is now needs-context, so the pause is not wedged above it.
        Assert.Equal(Corpus.Replace("  needs-context \"which colour?\"\n", "  needs-context \"which colour?\"\n  paused\n", StringComparison.Ordinal), paused.Text);
        var parsed = _parser.Parse(paused.Text!, "t.beh");
        Assert.True(parsed[0].Paused);
        Assert.Equal("which colour?", parsed[0].NeedsContext);

        var resumed = _writer.SetPaused(paused.Text!, "BEH-A", false);

        Assert.Equal(Corpus, resumed.Text);
    }

    [Fact]
    public void Adjudicating_a_behaviour_keeps_its_needs_context_line()
    {
        var r = _writer.SetReview(Corpus, "BEH-A", "denied", "no");

        Assert.True(r.Ok);
        Assert.Equal("which colour?", _parser.Parse(r.Text!, "t.beh")[0].NeedsContext);
    }
}
