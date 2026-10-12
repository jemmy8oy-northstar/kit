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
/// <remarks>
/// <para>A heading names what kind of note it is (kit#118, 2026-10-12):</para>
/// <code>
/// ## 2026-10-12 16:33Z                       a note on the project
/// ## 2026-10-12 16:33Z · BEH-1               a note on a behaviour
/// ## 2026-10-12 16:33Z · new behaviour       a behaviour asked for in prose (BEH-NOTE-4)
/// ## 2026-10-12 16:33Z · resolve page:X.app  how a decision should go (BEH-NOTE-8)
/// </code>
/// <para>
/// The two kind words contain a space, which no behaviour id can, so neither can be mistaken for one.
/// Under a note, whoever folds it may write <c>### Questions</c> and the engineer replies under
/// <c>### Answer</c> (BEH-NOTE-5/6). Typed text has every <c>##</c> line escaped, <c>###</c>
/// included, so prose can never forge a question or another note.
/// </para>
/// </remarks>
public static partial class Notes
{
    /// <summary>The most a single note may hold — a ramble, not a document.</summary>
    public const int MaxLength = 20_000;

    /// <summary>The longest question-sheet key a resolution may name.</summary>
    public const int MaxKeyLength = 200;

    public const string NoteKind = "note";
    public const string RequestKind = "request";
    public const string ResolutionKind = "resolution";

    private const string RequestTag = "new behaviour";
    private const string ResolveTag = "resolve ";

    /// <summary>The file's opening lines, written once, when the first note is left.</summary>
    public static string Header(string app) =>
        $"# Notes on {app}\n\nLeft in Kit (kit#118). Fold each one into the spec or the code, and delete it in the same pull request.\n";

    /// <summary>
    /// <paramref name="existing"/> (null: no file yet) with one note added at the end. A line of the
    /// note that would read as a heading is escaped with <c>\</c>, so a note can never split itself
    /// in two or forge another note's date.
    /// </summary>
    public static string Append(string? existing, string app, string text, string? behaviour, DateTimeOffset at) =>
        Tagged(existing, app, text, behaviour is null ? null : " · " + behaviour, at);

    /// <summary>A behaviour asked for in prose (BEH-NOTE-4): no id, title or actor — the fold writes those.</summary>
    public static string AppendRequest(string? existing, string app, string text, DateTimeOffset at) =>
        Tagged(existing, app, text, " · " + RequestTag, at);

    /// <summary>How a decision on the question sheet should go (BEH-NOTE-8), tagged with its key.</summary>
    public static string AppendResolution(string? existing, string app, string text, string key, DateTimeOffset at) =>
        Tagged(existing, app, text, " · " + ResolveTag + key, at);

    private static string Tagged(string? existing, string app, string text, string? tag, DateTimeOffset at)
    {
        var sb = new StringBuilder(existing is null ? Header(app) : existing.TrimEnd('\n') + "\n");
        sb.Append('\n').Append("## ").Append(Stamp(at)).Append(tag).Append("\n\n");
        AppendTyped(sb, text);
        return sb.ToString();
    }

    /// <summary>
    /// <paramref name="existing"/> with an answer written at the end of note <paramref name="index"/>
    /// (BEH-NOTE-6), or null when that note is not there — or is not the one left at
    /// <paramref name="noteAt"/>, because the file changed under the caller.
    /// </summary>
    public static string? Reply(string? existing, int index, string noteAt, string text, DateTimeOffset at)
    {
        if (Locate(existing, index, noteAt) is not { } span)
        {
            return null;
        }

        var lines = Lines(existing!);
        var end = span.End;
        while (end > span.Start + 1 && lines[end - 1].Length == 0)
        {
            end--;
        }

        var sb = new StringBuilder("\n### Answer ").Append(Stamp(at)).Append("\n\n");
        AppendTyped(sb, text);
        var answer = Lines(sb.ToString().TrimEnd('\n'));

        // Then one blank line and whatever followed: the next note's heading, or nothing (the final newline).
        return Join(lines.Take(end).Concat(answer).Append("").Concat(lines.Skip(span.End)));
    }

    /// <summary>
    /// <paramref name="existing"/> without note <paramref name="index"/> (BEH-NOTE-7), or null when that
    /// note is not there or is not the one left at <paramref name="noteAt"/>. Everything else is
    /// byte-identical, the header included, so the commit shows exactly the one note leaving.
    /// </summary>
    public static string? Dismiss(string? existing, int index, string noteAt)
    {
        if (Locate(existing, index, noteAt) is not { } span)
        {
            return null;
        }

        var lines = Lines(existing!);
        var start = span.Start;

        // Take the blank line that separated it from the one above with it.
        while (start > 0 && lines[start - 1].Length == 0)
        {
            start--;
        }

        return Join(lines.Take(start).Append("").Concat(lines.Skip(span.End)));
    }

    /// <summary>Every note in the file, oldest first. Text before the first note's heading is the header, not a note.</summary>
    public static List<Note> Parse(string? text)
    {
        var notes = new List<Note>();
        if (text is null)
        {
            return notes;
        }

        var lines = Lines(text);
        foreach (var span in Spans(lines))
        {
            notes.Add(Read(lines, span));
        }

        return notes;
    }

    /// <summary>A behaviour id as a note may name one: no spaces, nothing that would break the heading.</summary>
    public static bool IsBehaviourId(string id) => BehaviourId().IsMatch(id);

    /// <summary>A question-sheet key as a resolution may name one: one token, no line break.</summary>
    public static bool IsQuestionKey(string key) => key.Length <= MaxKeyLength && QuestionKey().IsMatch(key);

    private static Note Read(string[] lines, (int Start, int End) span)
    {
        var h = Heading().Match(lines[span.Start]);
        var kind = h.Groups[2].Success ? RequestKind : h.Groups[3].Success ? ResolutionKind : NoteKind;

        var body = new List<string>();
        var thread = new List<NoteEntry>();
        var opening = "";
        (string Kind, string? At)? section = null;
        for (var i = span.Start + 1; i < span.End; i++)
        {
            var line = lines[i];
            var s = Section().Match(line);
            if (s.Success)
            {
                Close();
                section = (s.Groups[1].Value == "Questions" ? "questions" : "answer", s.Groups[2].Success ? s.Groups[2].Value : null);
                continue;
            }

            body.Add(Escaped().IsMatch(line) && line[0] == '\\' ? line[1..] : line);
        }

        Close();
        return new Note
        {
            At = h.Groups[1].Value,
            Kind = kind,
            Behaviour = h.Groups[4].Success ? h.Groups[4].Value : null,
            Resolves = h.Groups[3].Success ? h.Groups[3].Value : null,
            Text = opening,
            Thread = thread,
            Questions = thread.Count > 0 && thread[^1].Kind == "questions" ? QuestionsIn(thread[^1].Text) : [],
        };

        // The note's own text runs to its first section; each section runs to the next.
        void Close()
        {
            if (section is { } open)
            {
                thread.Add(new NoteEntry { Kind = open.Kind, At = open.At, Text = Trimmed(body) });
            }
            else
            {
                opening = Trimmed(body);
            }

            body.Clear();
        }
    }

    /// <summary>The numbered (or bulleted) lines of a round of questions; a round with no list is one question.</summary>
    private static List<string> QuestionsIn(string text)
    {
        var items = new List<string>();
        foreach (var line in Lines(text))
        {
            var m = ListItem().Match(line);
            if (m.Success)
            {
                items.Add(m.Groups[1].Value.Trim());
            }
            else if (items.Count > 0 && line.Trim().Length > 0)
            {
                items[^1] += " " + line.Trim();
            }
        }

        return items.Count > 0 ? items : text.Trim().Length > 0 ? [text.Trim()] : [];
    }

    /// <summary>Where each note starts (its heading) and ends (the next heading, or the end of the file).</summary>
    private static List<(int Start, int End)> Spans(string[] lines)
    {
        var starts = new List<int>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (Heading().IsMatch(lines[i]))
            {
                starts.Add(i);
            }
        }

        return starts.Select((s, k) => (s, k + 1 < starts.Count ? starts[k + 1] : lines.Length)).ToList();
    }

    private static (int Start, int End)? Locate(string? text, int index, string noteAt)
    {
        if (text is null || index < 0)
        {
            return null;
        }

        var lines = Lines(text);
        var spans = Spans(lines);
        if (index >= spans.Count)
        {
            return null;
        }

        return Heading().Match(lines[spans[index].Start]).Groups[1].Value == noteAt ? spans[index] : null;
    }

    private static void AppendTyped(StringBuilder sb, string text)
    {
        foreach (var line in Lines(text.Trim()))
        {
            sb.Append(Escaped().IsMatch(line) ? "\\" + line : line).Append('\n');
        }
    }

    private static string Trimmed(List<string> body) => string.Join('\n', body).Trim('\n');

    private static string Join(IEnumerable<string> lines) => string.Join('\n', lines);

    private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm'Z'", CultureInfo.InvariantCulture);

    private static string[] Lines(string text) => text.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    [GeneratedRegex(@"^## (\d{4}-\d{2}-\d{2} \d{2}:\d{2}Z)(?: · (?:(new behaviour)|resolve (\S{1,200})|([A-Za-z0-9._-]+)))?$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^### (Questions|Answer)(?: (\d{4}-\d{2}-\d{2} \d{2}:\d{2}Z))?$")]
    private static partial Regex Section();

    [GeneratedRegex(@"^\s*(?:\d+[.)]|[-*])\s+(.*)$")]
    private static partial Regex ListItem();

    // "##" behind any run of backslashes: written with one more, read with one fewer, so every line round-trips.
    [GeneratedRegex(@"^\\*##")]
    private static partial Regex Escaped();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex BehaviourId();

    [GeneratedRegex(@"^\S+$")]
    private static partial Regex QuestionKey();
}
