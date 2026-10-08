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
