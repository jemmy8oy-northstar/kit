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

    public static string Line(string mode) => $"# kit:idle {mode}";

    /// <summary>The 0-based line numbers of every <c>kit:idle</c> line, however malformed.</summary>
    public static IEnumerable<int> Lines(string[] lines) =>
        Enumerable.Range(0, lines.Length).Where(i => Directive.IsMatch(lines[i].TrimEnd('\r')));

    /// <summary>The chosen mode: the first well-formed <c>kit:idle</c> line, else the default.</summary>
    public static string Read(string src)
    {
        foreach (var line in src.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var m = Directive.Match(line);
            if (m.Success && m.Groups[1].Value.Trim(CorpusParser.WsChars) is var mode && Modes.Contains(mode))
            {
                return mode;
            }
        }

        return Default;
    }
}
