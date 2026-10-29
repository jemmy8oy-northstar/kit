using System.Text.RegularExpressions;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c># kit:promote self</c> (BEH-SETTINGS-4): the project's grant for the agent to merge its
/// <c>dev</c> into <c>main</c> itself. Read from the corpus text like <c>kit:repo</c>, so the
/// parser is untouched and every change to the switch is a commit he can see.
///
/// A project may span several files (<c># kit:project kit</c> on kit.beh, kit-ui.beh and
/// kit-hosted.beh), so the rule is one unambiguous place: the project is granted iff its
/// PRIMARY file carries the line — the file named for the project (kit.beh for kit; the
/// only file, for a project of one). The same line in any other file of the project is
/// ignored, and a project with no file named for it cannot be granted. Absent, or anything
/// but <c>self</c> after the keyword, is "not granted": a grant must be spelled exactly.
/// </summary>
public static class PromoteDirective
{
    public const string Line = "# kit:promote self";

    private static readonly Regex Directive = new(
        $@"^#[{CorpusParser.Ws}]*kit:promote(?![A-Za-z0-9_-])(.*)\z", RegexOptions.Compiled);

    /// <summary>The 0-based line numbers of every <c>kit:promote</c> line, however malformed.</summary>
    public static IEnumerable<int> Lines(string[] lines) =>
        Enumerable.Range(0, lines.Length).Where(i => Directive.IsMatch(lines[i].TrimEnd('\r')));

    /// <summary>True when the file has a <c>kit:promote self</c> line.</summary>
    public static bool IsGranted(string src)
    {
        foreach (var line in src.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var m = Directive.Match(line);
            if (m.Success && m.Groups[1].Value.Trim(CorpusParser.WsChars) == "self")
            {
                return true;
            }
        }

        return false;
    }
}
