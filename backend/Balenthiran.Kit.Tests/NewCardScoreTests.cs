using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-BOARD-20 (his kit.notes.md 2026-10-25 12:34Z, "every item should be scored"): a card added
/// through Kit comes in at 4, so it is never unscored while it waits for the agent's next wake.
/// </summary>
public class NewCardScoreTests
{
    private const string Corpus = "behaviour BEH-A \"a\"\n  then sees field:F\n";

    private readonly ICorpusParser _parser = new CorpusParser();

    private readonly CorpusWriter _writer = new(new CorpusParser());

    [Fact]
    public void A_new_card_comes_in_scored_at_four()
    {
        var r = _writer.AddBehaviour(Corpus, "BEH-B", "b", steps: ["then sees field:G"]);

        Assert.True(r.Ok);
        var parsed = _parser.Parse(r.Text!, "t.beh");
        Assert.Equal(4, parsed[1].Priority);
        // The existing card is not scored by the write: only the new block is.
        Assert.Null(parsed[0].Priority);
    }

    [Fact]
    public void A_priority_the_caller_gives_wins_over_the_default()
    {
        var r = _writer.AddBehaviour(Corpus, "BEH-B", "b", steps: ["priority 2", "then sees field:G"]);

        Assert.True(r.Ok, r.Reason);
        Assert.Equal(2, _parser.Parse(r.Text!, "t.beh")[1].Priority);
    }
}
