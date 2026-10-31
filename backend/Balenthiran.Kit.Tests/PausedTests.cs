using System.Text.Json;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The <c>paused</c> line (BEH-BOARD-16), approving a paused card resumes it (BEH-BOARD-25), and
/// <c>paused until</c> keeps it out of his Inbox until its date (BEH-BOARD-26). Fixtures only: no committed corpus may carry one until
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

        Assert.Equal("t.beh:2: paused takes nothing after it but `until YYYY-MM-DD`, got: later", ex.Message);
    }

    [Fact]
    public void BEH_BOARD_26_a_bare_paused_has_no_date_and_a_dated_one_carries_it()
    {
        var parsed = _parser.Parse("behaviour BEH-A \"a\"\n  paused\n  then sees field:F\nbehaviour BEH-B \"b\"\n  paused until 2026-11-14\n  then sees field:F\n", "t.beh");

        Assert.True(parsed[0].Paused);
        Assert.Null(parsed[0].PausedUntil);
        Assert.True(parsed[1].Paused);
        Assert.Equal("2026-11-14", parsed[1].PausedUntil);

        var json = JsonDocument.Parse(new EngineJsonSerialiser().Serialise(parsed)).RootElement;
        Assert.False(json[0].TryGetProperty("pausedUntil", out _));
        Assert.Equal("2026-11-14", json[1].GetProperty("pausedUntil").GetString());
    }

    [Theory]
    [InlineData("until")]
    [InlineData("until soon")]
    [InlineData("until 2026-13-01")]
    [InlineData("until 2026-1-1")]
    [InlineData("until 2026-11-14 x")]
    [InlineData("2026-11-14")]
    public void BEH_BOARD_26_a_malformed_paused_until_is_refused(string rest)
    {
        Assert.Throws<CorpusParseException>(() => _parser.Parse($"behaviour BEH-A \"a\"\n  paused {rest}\n", "t.beh"));
    }

    [Fact]
    public void BEH_BOARD_26_pausing_writes_the_date_repausing_replaces_it_and_resuming_removes_the_line()
    {
        var first = _writer.SetPaused(Corpus, "BEH-A", true, "2026-11-14");
        Assert.True(first.Ok);
        Assert.Equal(Corpus.Replace("  priority 2\n", "  priority 2\n  paused until 2026-11-14\n", StringComparison.Ordinal), first.Text);

        var later = _writer.SetPaused(first.Text!, "BEH-A", true, "2026-12-01");
        Assert.Equal(Corpus.Replace("  priority 2\n", "  priority 2\n  paused until 2026-12-01\n", StringComparison.Ordinal), later.Text);
        Assert.Equal("2026-12-01", _parser.Parse(later.Text!, "t.beh")[0].PausedUntil);

        Assert.Equal(Corpus, _writer.SetPaused(later.Text!, "BEH-A", false).Text);
    }

    [Fact]
    public void BEH_BOARD_26_a_bad_date_is_refused_by_the_writer()
    {
        Assert.Equal("bad-date", _writer.SetPaused(Corpus, "BEH-A", true, "soon").Error);
    }

    [Fact]
    public void BEH_BOARD_26_the_pause_route_dates_it_fourteen_days_from_the_injected_today()
    {
        var root = Directory.CreateTempSubdirectory("kit-paused-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "demo.beh"), Corpus);
            var corpora = new Balenthiran.Kit.Database.CorpusDirectory(root, root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            var router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), null, clock: new FixedClock());

            var res = router.Route("POST", "/api/projects/demo/behaviours/BEH-A/paused", body: JsonDocument.Parse("""{"paused":true}""").RootElement);

            Assert.Equal(200, res.Status);
            Assert.Equal("2026-11-14", _parser.Parse(File.ReadAllText(Path.Combine(root, "demo.beh")), "t.beh")[0].PausedUntil);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 31, 23, 30, 0, TimeSpan.Zero);
    }

    private const string PausedApproved = "behaviour BEH-A \"a\"\n  source defined x\n  review unreviewed\n  paused until 2026-11-14\n  then sees field:F\n";

    [Fact]
    public void BEH_BOARD_25_approving_a_paused_card_with_nothing_blocking_it_resumes_it_in_the_same_write()
    {
        var r = _writer.SetReview(PausedApproved, "BEH-A", "approved");

        Assert.True(r.Ok);
        Assert.Equal("behaviour BEH-A \"a\"\n  source defined x\n  review approved\n  then sees field:F\n", r.Text);
        Assert.False(_parser.Parse(r.Text!, "t.beh")[0].Paused);
    }

    [Fact]
    public void BEH_BOARD_25_a_needs_context_line_keeps_an_approved_card_paused()
    {
        var text = PausedApproved.Replace("  paused until 2026-11-14\n", "  paused\n  needs-context \"which db\"\n", StringComparison.Ordinal);
        var r = _writer.SetReview(text, "BEH-A", "approved");

        Assert.True(r.Ok);
        Assert.Equal(text.Replace("review unreviewed", "review approved", StringComparison.Ordinal), r.Text);
        Assert.True(_parser.Parse(r.Text!, "t.beh")[0].Paused);
    }

    [Fact]
    public void BEH_BOARD_25_denying_a_paused_card_or_approving_an_unpaused_one_leaves_pause_as_it_was()
    {
        var denied = _writer.SetReview(PausedApproved, "BEH-A", "denied", "wrong");
        Assert.Equal(PausedApproved.Replace("review unreviewed", "review denied wrong",StringComparison.Ordinal), denied.Text);

        var plain = _writer.SetReview(Corpus, "BEH-B", "approved");
        Assert.Equal(Corpus.Replace("behaviour BEH-B \"b\"\n", "behaviour BEH-B \"b\"\n  review approved\n", StringComparison.Ordinal), plain.Text);
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
