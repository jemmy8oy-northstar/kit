using System.Text.RegularExpressions;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c># kit:conflicts build|ask|wait</c> (BEH-CONFLICT-2): what the agent does when two specs
/// conflict. <c>build</c> builds one reading anyway and asks him to confirm it when approving (the
/// default, stored as NO line), <c>ask</c> asks him before building the conflicting card, and
/// <c>wait</c> does no work on it until he resolves it. Modelled on <see cref="PromoteDirective"/>:
/// read from the corpus text, only the PRIMARY file of a project counts, and anything not spelled
/// exactly is skipped, so it reads as the default.
/// </summary>
public static class ConflictsDirective
{
    public const string Build = "build";

    public const string Ask = "ask";

    public const string Wait = "wait";

    public static readonly IReadOnlyList<string> Modes = [Build, Ask, Wait];

    private static readonly Regex Directive = new(
        $@"^#[{CorpusParser.Ws}]*kit:conflicts(?![A-Za-z0-9_-])(.*)\z", RegexOptions.Compiled);

    public static string Line(string mode) => $"# kit:conflicts {mode}";

    /// <summary>The 0-based line numbers of every <c>kit:conflicts</c> line, however malformed.</summary>
    public static IEnumerable<int> Lines(string[] lines) =>
        Enumerable.Range(0, lines.Length).Where(i => Directive.IsMatch(lines[i].TrimEnd('\r')));

    /// <summary>The mode the file chooses: its first well-formed line, else <c>build</c>.</summary>
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

        return Build;
    }
}
