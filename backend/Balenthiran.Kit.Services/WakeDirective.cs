using System.Globalization;
using System.Text.RegularExpressions;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c># kit:wake &lt;ISO 8601 UTC time&gt;</c> (BEH-SETTINGS-7): when the operator last asked the agent
/// to wake, kept in the header of Kit's own primary corpus file like <c>kit:idle</c>. The line is exactly
/// <c># kit:wake &lt;iso&gt;</c> with nothing after, because an external scheduler reads it with
/// <c>grep -E '^# kit:wake [0-9]'</c> and takes the third whitespace field as the time.
/// </summary>
public static class WakeDirective
{
    /// <summary>The corpus that carries the setting: Kit's own primary file.</summary>
    public const string App = "kit";

    private static readonly Regex Directive = new(
        $@"^#[{CorpusParser.Ws}]*kit:wake(?![A-Za-z0-9_-])(.*)\z", RegexOptions.Compiled);

    public static string Line(DateTimeOffset at) =>
        $"# kit:wake {at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}";

    /// <summary>The 0-based line numbers of every <c>kit:wake</c> line, however malformed.</summary>
    public static IEnumerable<int> Lines(string[] lines) =>
        Enumerable.Range(0, lines.Length).Where(i => Directive.IsMatch(lines[i].TrimEnd('\r')));

    /// <summary>The time on the first well-formed <c>kit:wake</c> line, else null.</summary>
    public static string? Read(string src)
    {
        foreach (var line in src.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var m = Directive.Match(line);
            var value = m.Success ? m.Groups[1].Value.Trim(CorpusParser.WsChars) : "";
            if (value.Length > 0
                && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at))
            {
                return Line(at)["# kit:wake ".Length..];
            }
        }

        return null;
    }
}
