using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c>review requested &lt;what to look at&gt;</c> (kit#284): built and merged, and the agent
/// would like his eyes on it. No committed corpus has one, so these are the value and the refusal.
/// </summary>
public class ReviewRequestedTests
{
    private readonly ICorpusParser _parser = new CorpusParser();

    [Fact]
    public void A_review_request_is_read_with_what_to_look_at()
    {
        var parsed = _parser.Parse("behaviour BEH-A \"a\"\n  priority 2\n  review requested the empty state wording\n", "t.beh");

        Assert.Equal("requested", parsed[0].Review.State);
        Assert.Equal("the empty state wording", parsed[0].Review.Note);
    }

    [Fact]
    public void A_review_request_that_does_not_say_what_to_look_at_is_refused()
    {
        var ex = Assert.Throws<CorpusParseException>(() => _parser.Parse("behaviour BEH-A \"a\"\n  review requested\n", "t.beh"));

        Assert.Equal("t.beh:2: a review request must say what to look at", ex.Message);
    }

    [Fact]
    public void A_request_on_an_inference_is_not_overwritten_back_to_unreviewed()
    {
        var parsed = _parser.Parse("behaviour BEH-A \"a\"\n  review requested the copy\n  source inferred kit#284\n", "t.beh");

        Assert.Equal("requested", parsed[0].Review.State);
    }
}
