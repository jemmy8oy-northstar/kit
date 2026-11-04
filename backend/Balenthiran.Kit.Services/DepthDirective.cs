using System.Text.RegularExpressions;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c># kit:depth goals|behaviours|architecture</c> (BEH-ARCH-3): how involved the user chooses to be.
/// <c>goals</c> leaves the behaviours to Kit, <c>behaviours</c> is the user deciding what the app does
/// (the default, stored as NO line), and <c>architecture</c> adds how it is built. Modelled on
/// <see cref="ConflictsDirective"/>: read from the corpus text, only the PRIMARY file of a project
/// counts, and anything not spelled exactly is skipped, so it reads as the default.
/// </summary>
public static class DepthDirective
{
    public const string Goals = "goals";

    public const string Behaviours = "behaviours";

    public const string Architecture = "architecture";

    public static readonly IReadOnlyList<string> Modes = [Goals, Behaviours, Architecture];

    private static readonly Regex Directive = new(
        $@"^#[{CorpusParser.Ws}]*kit:depth(?![A-Za-z0-9_-])(.*)\z", RegexOptions.Compiled);

    public static string Line(string mode) => $"# kit:depth {mode}";

    /// <summary>The 0-based line numbers of every <c>kit:depth</c> line, however malformed.</summary>
    public static IEnumerable<int> Lines(string[] lines) =>
        Enumerable.Range(0, lines.Length).Where(i => Directive.IsMatch(lines[i].TrimEnd('\r')));

    /// <summary>The depth the file chooses: its first well-formed line, else <c>behaviours</c>.</summary>
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

        return Behaviours;
    }

    /// <summary>The sentence added to the spec agent's instructions for this depth; empty for the default.</summary>
    public static string Instruction(string mode) => mode switch
    {
        Goals => "This user works at the level of goals: do not ask them to decide individual behaviours; propose them yourself, "
            + "saved as notes for the coding agent to file as unreviewed suggestions, and only ask about goals and priorities.",
        Architecture => "This user also wants to decide architecture: raise stack and design choices with them rather than deciding them.",
        _ => "",
    };
}
