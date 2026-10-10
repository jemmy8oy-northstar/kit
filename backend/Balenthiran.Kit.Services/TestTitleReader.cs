using System.Text;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Port of <c>kit.js</c>'s test reader: <c>testTitles</c>, <c>expectedTestCount</c>,
/// <c>jsDeclarationCount</c> and the skip helpers. Scored against Node's own answers in
/// <c>Fixtures/Check/goldens.json</c>.
/// </summary>
/// <remarks>
/// ⚠️ THE REGEXES ARE JAVASCRIPT'S, NOT .NET'S. <c>\s</c>, <c>\w</c>, <c>\b</c>, <c>.</c> and a
/// multiline <c>^</c> all mean something different in .NET, so each is spelled out below as
/// JavaScript reads it. The goldens hold a case for every one of them (a BOM, a no-break
/// space, U+2028, a non-ASCII method name).
///
/// Two counts, deliberately unlike each other: <see cref="Titles"/> keys on POSITION (a
/// declaration starts a statement); <see cref="ExpectedCount"/> keys on LEXICAL STRUCTURE
/// (strip strings and comments, then count call heads anywhere). If they were the same idea
/// twice, agreement would prove nothing.
/// </remarks>
public sealed class TestTitleReader : ITestTitleReader
{
    // JavaScript's \s: WhiteSpace plus LineTerminator. .NET's \s differs (no U+FEFF, extra U+0085).
    private const string Ws = @"[\t\n\v\f\r \u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000\ufeff]";

    // JavaScript's `.`: anything but a LineTerminator.
    private const string Dot = @"[^\n\r\u2028\u2029]";

    // JavaScript's \w is ASCII only.
    private const string Word = "[A-Za-z0-9_]";

    private static readonly Regex TestFile = new(@"\.(spec|test)\.(ts|tsx|js|jsx)\z|Tests?\.cs\z", RegexOptions.CultureInvariant);

    // A declaration begins a statement; a fixture string never does. JavaScript's multiline ^
    // also matches after \r, U+2028 and U+2029, which .NET's does not.
    private static readonly Regex JsDecl = new(
        @"(?:^|(?<=[\r\u2028\u2029]))[ \t]*(?:await" + Ws + "+|return" + Ws + @"+)?(?:test|it)(?:\.(?:only|skip|fixme|concurrent|each))?" + Ws + "*[(`]",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex JsQuoted = new(@"\G" + Ws + @"*(['""`])((?:\\" + Dot + @"|(?!\1)[^\\])*)\1", RegexOptions.CultureInvariant);

    private static readonly Regex CsAttr = new(@"\[(?:Fact|Theory)[^\]]*\]", RegexOptions.CultureInvariant);

    private static readonly Regex CsDisplay = new(@"DisplayName" + Ws + "*=" + Ws + @"*""((?:\\" + Dot + @"|[^""\\])*)""", RegexOptions.CultureInvariant);

    private static readonly Regex CsMethod = new(
        "^" + Ws + "*(?:public|internal)" + Ws + "+(?:async" + Ws + @"+)?(?:[A-Za-z0-9_<>,\[\]?]|" + Ws + ")+?" + Ws + "(" + Word + "+)" + Ws + @"*\(",
        RegexOptions.CultureInvariant);

    // A line that can sit between an attribute and its method: another attribute, a comment, a blank.
    private static readonly Regex CsSkip = new("^" + Ws + @"*(?:\[|//|/\*|\*|\z)", RegexOptions.CultureInvariant);

    private static readonly Regex CsCount = new(@"\[(?:Fact|Theory)(?!" + Word + ")", RegexOptions.CultureInvariant);

    // ⚠️ The lookbehind is load-bearing: without it every `SOME_RE.test(x)` counts as a test.
    private static readonly Regex JsCallHead = new(
        @"(?<![.A-Za-z0-9_$])(?:test|it)(?:\.(?:only|skip|fixme|concurrent|each))?" + Ws + @"*[(\[`]",
        RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public bool IsTestFile(string name) => TestFile.IsMatch(name);

    /// <inheritdoc />
    public IReadOnlyList<ITestTitle> Titles(string file, string src)
    {
        var output = new List<ITestTitle>();
        if (!file.EndsWith(".cs", StringComparison.Ordinal))
        {
            foreach (Match m in JsDecl.Matches(src))
            {
                var line = src.AsSpan(0, m.Index).Count('\n') + 1;
                var isEach = m.Value.Contains(".each", StringComparison.Ordinal);

                // For `.each` the title sits after the table: skip the table (or tagged
                // template), and the title is the next call's first argument.
                var at = m.Index + m.Length - 1;
                if (isEach)
                {
                    var past = SkipGroup(src, at);
                    if (past < 0)
                    {
                        break;
                    }

                    var k = past;
                    while (k < src.Length && IsJsWhitespace(src[k]))
                    {
                        k++;
                    }

                    if (k >= src.Length || src[k] != '(')
                    {
                        continue;
                    }

                    at = k;
                }

                // A computed title is skipped here and still counted by the second count,
                // so it refuses rather than vanishing.
                var q = JsQuoted.Match(src, at + 1);
                if (!q.Success)
                {
                    continue;
                }

                output.Add(new TestTitle { File = file, Line = line, Raw = q.Groups[2].Value, Style = isEach ? "each" : "title" });
            }

            return output;
        }

        // xUnit: the method names the test unless a DisplayName overrides it. The first
        // line after the attribute that is not another attribute or a comment settles it.
        var lines = src.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!CsAttr.IsMatch(lines[i]))
            {
                continue;
            }

            var display = CsDisplay.Match(lines[i]);
            for (var j = i + 1; j < lines.Length; j++)
            {
                if (CsSkip.IsMatch(lines[j]))
                {
                    continue;
                }

                var m = CsMethod.Match(lines[j]);
                if (m.Success)
                {
                    output.Add(new TestTitle
                    {
                        File = file,
                        Line = j + 1,
                        Raw = display.Success ? display.Groups[1].Value : m.Groups[1].Value,
                        Style = display.Success ? "DisplayName" : "method",
                    });
                }

                break;
            }
        }

        return output;
    }

    /// <inheritdoc />
    public int ExpectedCount(string file, string src) =>
        file.EndsWith(".cs", StringComparison.Ordinal) ? CsCount.Matches(src).Count : JsDeclarationCount(src);

    private static int JsDeclarationCount(string src)
    {
        var output = new StringBuilder(src.Length);
        for (var i = 0; i < src.Length; i++)
        {
            var c = src[i];
            if (c == '"' || c == '\'')
            {
                var e = SkipQuoted(src, i);
                output.Append(' ');
                if (e >= 0)
                {
                    i = e;
                }

                continue;
            }

            if (c == '`')
            {
                var e = SkipTemplate(src, i);
                if (e < 0)
                {
                    output.Append(' ');
                    continue;
                }

                // Keep the backtick, blank the body: `it.each` plus a tagged-template table is
                // a declaration, and the call head needs the backtick to see it.
                output.Append('`');
                i = e - 1;
                continue;
            }

            if (c == '/' && At(src, i + 1) == '/')
            {
                var e = src.IndexOf('\n', i);
                if (e < 0)
                {
                    break;
                }

                output.Append(' ');
                i = e - 1;
                continue;
            }

            if (c == '/' && At(src, i + 1) == '*')
            {
                var e = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (e < 0)
                {
                    break;
                }

                output.Append(' ');
                i = e + 1;
                continue;
            }

            output.Append(c);
        }

        return JsCallHead.Matches(output.ToString()).Count;
    }

    // Past one balanced bracket group or template literal, quoted text opaque. -1 if it never closes.
    private static int SkipGroup(string src, int i)
    {
        while (i < src.Length && IsJsWhitespace(src[i]))
        {
            i++;
        }

        var open = At(src, i);
        if (open == '`')
        {
            return SkipTemplate(src, i);
        }

        if (open != '(' && open != '[')
        {
            return -1;
        }

        var close = open == '(' ? ')' : ']';
        var depth = 0;
        for (; i < src.Length; i++)
        {
            var c = src[i];
            if (c == '"' || c == '\'')
            {
                i = SkipQuoted(src, i);
                if (i < 0)
                {
                    return -1;
                }

                continue;
            }

            if (c == '`')
            {
                i = SkipTemplate(src, i) - 1;
                if (i < 0)
                {
                    return -1;
                }

                continue;
            }

            if (c == '/' && At(src, i + 1) == '/')
            {
                i = src.IndexOf('\n', i);
                if (i < 0)
                {
                    return src.Length;
                }

                continue;
            }

            if (c == '/' && At(src, i + 1) == '*')
            {
                var e = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (e < 0)
                {
                    return -1;
                }

                i = e + 1;
                continue;
            }

            if (c == open || (open == '(' && c == '[') || (open == '[' && c == '('))
            {
                depth++;
            }
            else if (c == close || (open == '(' && c == ']') || (open == '[' && c == ')'))
            {
                depth--;
                if (depth == 0)
                {
                    return i + 1;
                }
            }
        }

        return -1;
    }

    // `i` is at the opening quote; the index OF the closing one, or -1.
    private static int SkipQuoted(string src, int i)
    {
        var q = src[i];
        for (var j = i + 1; j < src.Length; j++)
        {
            if (src[j] == '\\')
            {
                j++;
                continue;
            }

            if (src[j] == q)
            {
                return j;
            }

            if (q != '`' && src[j] == '\n')
            {
                return -1;
            }
        }

        return -1;
    }

    // `i` is at the backtick; the index just PAST the closing one, or -1. `${}` is skipped
    // as a balanced group so a brace inside it cannot end the literal.
    private static int SkipTemplate(string src, int i)
    {
        for (var j = i + 1; j < src.Length; j++)
        {
            if (src[j] == '\\')
            {
                j++;
                continue;
            }

            if (src[j] == '$' && At(src, j + 1) == '{')
            {
                var depth = 0;
                for (; j < src.Length; j++)
                {
                    if (src[j] == '{')
                    {
                        depth++;
                    }
                    else if (src[j] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            break;
                        }
                    }
                }

                continue;
            }

            if (src[j] == '`')
            {
                return j + 1;
            }
        }

        return -1;
    }

    // `src[i]` as JavaScript reads it: past the end is undefined, which equals no character.
    private static char At(string src, int i) => i >= 0 && i < src.Length ? src[i] : '\0';

    private static bool IsJsWhitespace(char c) => c switch
    {
        '\t' or '\n' or '\v' or '\f' or '\r' or ' ' or '\u00a0' or '\u1680' or '\u2028' or '\u2029' or '\u202f' or '\u205f' or '\u3000' or '\ufeff' => true,
        _ => c >= '\u2000' && c <= '\u200a',
    };
}
