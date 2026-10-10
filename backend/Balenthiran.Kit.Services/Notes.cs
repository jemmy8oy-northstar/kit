using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c>&lt;app&gt;.notes.md</c> (kit#118): free text left in Kit, appended under a dated heading, and
/// deleted by whoever folds it into the spec. Plain markdown so it reads on GitHub as it is, and
/// NOT part of the corpus — nothing parses a note as a behaviour or generates from it.
/// </summary>
public static partial class Notes
{
    /// <summary>The most a single note may hold — a ramble, not a document.</summary>
    public const int MaxLength = 20_000;

    /// <summary>The file's opening lines, written once, when the first note is left.</summary>
    public static string Header(string app) =>
        $"# Notes on {app}\n\nLeft in Kit (kit#118). Fold each one into the spec or the code, and delete it in the same pull request.\n";

    /// <summary>
    /// <paramref name="existing"/> (null: no file yet) with one note added at the end. A line of the
    /// note that would read as a heading is escaped with <c>\</c>, so a note can never split itself
    /// in two or forge another note's date.
    /// </summary>
    public static string Append(string? existing, string app, string text, string? behaviour, DateTimeOffset at)
    {
        var sb = new StringBuilder(existing is null ? Header(app) : existing.TrimEnd('\n') + "\n");
        sb.Append('\n').Append("## ").Append(Stamp(at));
        if (behaviour is not null)
        {
            sb.Append(" · ").Append(behaviour);
        }

        sb.Append("\n\n");
        foreach (var line in Lines(text.Trim()))
        {
            sb.Append(Escaped().IsMatch(line) ? "\\" + line : line).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>Every note in the file, oldest first. Text before the first note's heading is the header, not a note.</summary>
    public static List<Note> Parse(string? text)
    {
        var notes = new List<Note>();
        if (text is null)
        {
            return notes;
        }

        (string At, string? Behaviour)? open = null;
        var body = new List<string>();
        foreach (var line in Lines(text.TrimStart('﻿')))
        {
            var m = Heading().Match(line);
            if (m.Success)
            {
                Close();
                open = (m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null);
                continue;
            }

            body.Add(Escaped().IsMatch(line) && line[0] == '\\' ? line[1..] : line);
        }

        Close();
        return notes;

        void Close()
        {
            if (open is { } o)
            {
                notes.Add(new Note { At = o.At, Behaviour = o.Behaviour, Text = string.Join('\n', body).Trim('\n') });
            }

            body.Clear();
        }
    }

    /// <summary>A behaviour id as a note may name one: no spaces, nothing that would break the heading.</summary>
    public static bool IsBehaviourId(string id) => BehaviourId().IsMatch(id);

    private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm'Z'", CultureInfo.InvariantCulture);

    private static string[] Lines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    [GeneratedRegex(@"^## (\d{4}-\d{2}-\d{2} \d{2}:\d{2}Z)(?: · ([A-Za-z0-9._-]+))?$")]
    private static partial Regex Heading();

    // "##" behind any run of backslashes: written with one more, read with one fewer, so every line round-trips.
    [GeneratedRegex(@"^\\*##")]
    private static partial Regex Escaped();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex BehaviourId();
}
