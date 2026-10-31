using System.Text.RegularExpressions;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c># kit:switch FEAT-X operator|testers on|off</c> (BEH-FLAGS-1): whether one of Kit's own
/// features is on for the operator or for test users. Read from the header of Kit's primary corpus
/// file like <c>kit:promote</c> and <c>kit:idle</c>, and stored as a comment, so every parser
/// (production's included) ignores it and every change is a commit he can see.
///
/// No line means the default: on for the operator, off for testers, so an experiment reaches him
/// first. A line that is not spelled exactly is skipped; for a feature and group the first
/// well-formed line wins.
/// </summary>
public static class SwitchDirective
{
    /// <summary>The corpus that carries the switches: Kit's own primary file.</summary>
    public const string App = IdleDirective.App;

    public const string Operator = "operator";

    public const string Testers = "testers";

    public static readonly IReadOnlyList<string> Groups = [Operator, Testers];

    private static readonly Regex Directive = new(
        $@"^#[{CorpusParser.Ws}]*kit:switch(?![A-Za-z0-9_-])(.*)\z", RegexOptions.Compiled);

    private static readonly Regex FeatureId = new(@"^FEAT-[A-Za-z0-9-]+\z", RegexOptions.Compiled);

    public static bool IsFeatureId(string id) => FeatureId.IsMatch(id);

    /// <summary>What a feature with no line is, for the group.</summary>
    public static bool DefaultFor(string group) => group == Operator;

    public static string Line(string feature, string group, bool on) =>
        $"# kit:switch {feature} {group} {(on ? "on" : "off")}";

    private static (string Feature, string Group, bool On)? Parse(string line)
    {
        var m = Directive.Match(line.TrimEnd('\r'));
        if (!m.Success)
        {
            return null;
        }

        var parts = m.Groups[1].Value.Trim(CorpusParser.WsChars)
            .Split(CorpusParser.WsChars, StringSplitOptions.RemoveEmptyEntries);
        return parts is [var feature, var group, var state]
            && FeatureId.IsMatch(feature) && Groups.Contains(group) && state is "on" or "off"
            ? (feature, group, state == "on")
            : null;
    }

    /// <summary>The 0-based numbers of the well-formed lines for this feature and group.</summary>
    public static IEnumerable<int> Lines(string[] lines, string feature, string group) =>
        Enumerable.Range(0, lines.Length)
            .Where(i => Parse(lines[i]) is { } p && p.Feature == feature && p.Group == group);

    /// <summary>The 0-based number of the last <c>kit:switch</c> line, well-formed or not; -1 when none.</summary>
    public static int LastLine(string[] lines) =>
        Enumerable.Range(0, lines.Length).Where(i => Directive.IsMatch(lines[i].TrimEnd('\r'))).DefaultIfEmpty(-1).Max();

    /// <summary>Every explicit switch in the text, keyed by feature and group; the first line for a key wins.</summary>
    public static Dictionary<(string Feature, string Group), bool> Read(string src)
    {
        var found = new Dictionary<(string, string), bool>();
        foreach (var line in src.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (Parse(line) is { } p)
            {
                found.TryAdd((p.Feature, p.Group), p.On);
            }
        }

        return found;
    }

    /// <summary>Whether the feature is on for the group: the line when there is one, else the default.</summary>
    public static bool IsOn(IReadOnlyDictionary<(string Feature, string Group), bool> switches, string feature, string group) =>
        switches.TryGetValue((feature, group), out var on) ? on : DefaultFor(group);
}
