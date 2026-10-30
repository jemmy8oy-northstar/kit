using System.Text.RegularExpressions;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c># kit:idle &lt;mode&gt;</c> (BEH-SETTINGS-5): how busy the agent should be when nothing is
/// asked of it. Read from the header of Kit's own primary corpus file like <c>kit:promote</c>, so
/// the parser is untouched and every change is a commit he can see. Absent, or anything but one of
/// the four modes, is <see cref="Default"/>: a choice must be spelled exactly to count.
/// </summary>
public static class IdleDirective
{
    public const string Default = "health";

    /// <summary>The corpus that carries the setting: Kit's own primary file.</summary>
    public const string App = "kit";

    public static readonly IReadOnlyList<string> Modes = ["on-change", "health", "ideas", "idle"];

    private static readonly Regex Directive = new(
        $@"^#[{CorpusParser.Ws}]*kit:idle(?![A-Za-z0-9_-])(.*)\z", RegexOptions.Compiled);

    /// <summary>The two kinds of idle work (BEH-SETTINGS-6). Order is priority; one not listed is off.</summary>
    public static readonly IReadOnlyList<string> Items = ["health", "ideas"];

    public static string Line(IReadOnlyList<string> order) =>
        $"# kit:idle {(order.Count == 0 ? "none" : string.Join(", ", order))}";

    /// <summary>The order a legacy single mode meant (BEH-SETTINGS-5): <c>idle</c> and <c>on-change</c> are none.</summary>
    public static IReadOnlyList<string> OrderOfMode(string mode) =>
        mode is "health" or "ideas" ? [mode] : [];

    /// <summary>Parses the text after <c>kit:idle</c>; null when malformed.</summary>
    private static IReadOnlyList<string>? Parse(string value)
    {
        if (value is "idle" or "on-change" or "none")
        {
            return [];
        }

        var tokens = value.Split(',').Select(t => t.Trim(CorpusParser.WsChars)).ToList();
        return tokens.All(Items.Contains) && tokens.Distinct().Count() == tokens.Count ? tokens : null;
    }

    /// <summary>The 0-based line numbers of every <c>kit:idle</c> line, however malformed.</summary>
    public static IEnumerable<int> Lines(string[] lines) =>
        Enumerable.Range(0, lines.Length).Where(i => Directive.IsMatch(lines[i].TrimEnd('\r')));

    /// <summary>The idle work in his order: the first well-formed <c>kit:idle</c> line, else the default.</summary>
    public static IReadOnlyList<string> ReadOrder(string src)
    {
        foreach (var line in src.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var m = Directive.Match(line);
            if (m.Success && Parse(m.Groups[1].Value.Trim(CorpusParser.WsChars)) is { } order)
            {
                return order;
            }
        }

        return [Default];
    }

    /// <summary>Compatibility for BEH-SETTINGS-5 readers: the first item on, or <c>idle</c> when none is.</summary>
    public static string Read(string src) => ReadOrder(src) is [var first, ..] ? first : "idle";
}
