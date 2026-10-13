using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c>&lt;app&gt;.schedule</c> (BEH-SCHED-1..3, kit#118): the order to build an app's pending
/// behaviours in, one id per line, beside the corpus. A pending behaviour that is not listed is
/// backlog, and nothing in the <c>.beh</c> says which — so scheduling never touches the spec, and
/// reordering never shows up in its diff.
/// </summary>
/// <remarks>
/// Kit writes the whole file on every change. A line starting <c>#</c> is a comment and a blank line
/// is nothing, so a hand-edited file still reads; an id listed twice counts where it first appears.
/// </remarks>
public static class Schedule
{
    public const string Add = "add";
    public const string Remove = "remove";
    public const string Up = "up";
    public const string Down = "down";

    /// <summary>The ids in order, first first. No file is nothing scheduled.</summary>
    public static List<string> Parse(string? text)
    {
        var ids = new List<string>();
        if (text is null)
        {
            return ids;
        }

        foreach (var raw in text.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length > 0 && line[0] != '#' && !ids.Contains(line, StringComparer.Ordinal))
            {
                ids.Add(line);
            }
        }

        return ids;
    }

    /// <summary>The file for <paramref name="ids"/>: a short header, then one id per line.</summary>
    public static string Render(string app, IEnumerable<string> ids)
    {
        var sb = new StringBuilder()
            .Append("# The order to build ").Append(app).Append("'s pending behaviours in (kit#118). Kit writes this file.\n")
            .Append("# One id per line; the first one still pending is next. Pending and not listed = backlog.\n");
        foreach (var id in ids)
        {
            sb.Append(id).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// <paramref name="ids"/> after one change, or the reason it cannot be made. Adding appends;
    /// up and down swap neighbours; removing returns it to the backlog. Whether an added id is a
    /// pending behaviour is the caller's to check — this knows only the list.
    /// </summary>
    public static (List<string>? Ids, string? Error, string? Reason) Change(IReadOnlyList<string> ids, string action, string id)
    {
        var at = ids.ToList().IndexOf(id);
        var next = ids.ToList();
        switch (action)
        {
            case Add when at >= 0:
                return (null, "already-scheduled", $"{id} is already on the schedule, at position {at + 1}");
            case Add:
                next.Add(id);
                return (next, null, null);
            case Remove or Up or Down when at < 0:
                return (null, "not-scheduled", $"{id} is not on the schedule — it may have been moved or removed already. Reload to see the schedule.");
            case Remove:
                next.RemoveAt(at);
                return (next, null, null);
            case Up when at == 0:
                return (null, "cannot-move", $"{id} is already first on the schedule");
            case Down when at == ids.Count - 1:
                return (null, "cannot-move", $"{id} is already last on the schedule");
            case Up or Down:
                var to = action == Up ? at - 1 : at + 1;
                (next[at], next[to]) = (next[to], next[at]);
                return (next, null, null);
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "the router checks the action first");
        }
    }

    /// <summary>The commit message's summary for a change.</summary>
    public static string Summary(string action, string id) => action switch
    {
        Add => $"schedule {id}",
        Remove => $"return {id} to the backlog",
        _ => $"move {id} {action} the schedule",
    };
}
