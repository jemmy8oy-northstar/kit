using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The whitespace the parser splits and trims on is JavaScript's, not .NET's.
///
/// <see cref="CorpusParser"/> promised a test for this from its first commit and
/// none existed — the goldens cannot see it, because no corpus contains a
/// character the two runtimes disagree about. So these cases are written for
/// exactly those characters.
///
/// The oracle is Node itself, not a reading of the spec: every code point in
/// U+0000–U+FFFF was put through <c>/\s/.test</c> in Node 22, which matched
/// exactly these 25, and <c>String.prototype.trim</c> removes exactly the same
/// set. A list typed from memory would be the same guess on both sides.
/// </summary>
public class WhitespaceTests
{
    private readonly ICorpusParser _parser = new CorpusParser();

    /// <summary>Measured from Node 22 (see the class summary). <c>\n</c> is excluded: it splits lines.</summary>
    public static TheoryData<int> JavaScriptWhitespace() =>
    [
        0x0009, 0x000b, 0x000c, 0x000d, 0x0020, 0x00a0, 0x1680,
        0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200a,
        0x2028, 0x2029, 0x202f, 0x205f, 0x3000, 0xfeff,
    ];

    [Theory]
    [MemberData(nameof(JavaScriptWhitespace))]
    public void Every_JavaScript_whitespace_character_separates_and_trims(int codePoint)
    {
        var c = (char)codePoint;
        var corpus = $"{c}behaviour{c}BEH-X{c}\"t\"{c}\n{c}actor{c}someone{c}\n";

        var parsed = _parser.Parse(corpus);

        var b = Assert.Single(parsed);
        Assert.Equal("BEH-X", b.Id);
        Assert.Equal("someone", b.Actor);
    }

    /// <summary>
    /// The disagreement in the other direction. U+0085 (NEXT LINE) is
    /// whitespace to .NET's <c>\s</c> and to <see cref="string.Trim()"/>, and
    /// NOT to JavaScript — so Node reads <c>behaviour&lt;U+0085&gt;BEH-X</c> as
    /// one unknown word and rejects the line. A port using .NET's notion of
    /// whitespace would accept it, and this is the case that says so.
    /// </summary>
    [Fact]
    public void Next_line_is_not_whitespace_so_the_line_is_rejected_as_Node_rejects_it()
    {
        var corpus = "behaviour\u0085BEH-X \"t\"\n";

        var ex = Assert.Throws<CorpusParseException>(() => _parser.Parse(corpus));

        Assert.Contains("line outside a behaviour", ex.Message);
    }
}
