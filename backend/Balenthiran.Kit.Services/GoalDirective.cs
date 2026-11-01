using System.Text.RegularExpressions;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c># kit:goal maintained</c> (BEH-NARR-2): the project has no goal to work towards and is being
/// kept going: fix what breaks, build nothing new. Modelled exactly on <see cref="PromoteDirective"/>:
/// read from the corpus text, only the PRIMARY file of a project counts, and anything but
/// <c>maintained</c> after the keyword is "not marked".
/// </summary>
public static class GoalDirective
{
    public const string Line = "# kit:goal maintained";

    private static readonly Regex Directive = new(
        $@"^#[{CorpusParser.Ws}]*kit:goal(?![A-Za-z0-9_-])(.*)\z", RegexOptions.Compiled);

    /// <summary>The 0-based line numbers of every <c>kit:goal</c> line, however malformed.</summary>
    public static IEnumerable<int> Lines(string[] lines) =>
        Enumerable.Range(0, lines.Length).Where(i => Directive.IsMatch(lines[i].TrimEnd('\r')));

    /// <summary>True when the file has a <c>kit:goal maintained</c> line.</summary>
    public static bool IsMaintained(string src)
    {
        foreach (var line in src.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var m = Directive.Match(line);
            if (m.Success && m.Groups[1].Value.Trim(CorpusParser.WsChars) == "maintained")
            {
                return true;
            }
        }

        return false;
    }
}
