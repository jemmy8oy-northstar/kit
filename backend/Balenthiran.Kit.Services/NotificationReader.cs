using System.Globalization;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <see cref="INotificationReader"/> over the agent's feed file (BEH-NOTIFY-1): one JSON object a
/// line, <c>{"at", "kind", "text"}</c>, appended oldest first by the agent's <c>notify</c>.
/// <see cref="Recent"/> answers from the last read; <see cref="RefreshAsync"/> reads again, and is
/// what the server calls when the last read has expired.
/// </summary>
/// <param name="files">Reads the file with Kit's own GitHub credential; unused without a <paramref name="source"/>.</param>
/// <param name="source"><c>KIT_AGENT_FEED</c>; null means no feed is configured, and the page says so.</param>
/// <param name="scheduleSource"><c>KIT_AGENT_SCHEDULE</c> (BEH-NOTIFY-5): the agent's <c>state/suspend.json</c>; null means Kit cannot say when it wakes, and the page says so.</param>
public sealed class NotificationReader(IGitHubFileReader? files, IProjectSource? source, IProjectSource? scheduleSource = null) : INotificationReader
{
    /// <summary>The most entries the page shows.</summary>
    public const int Cap = 50;

    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal) { "wake", "sleep", "pr", "blocker", "done", "note" };

    private volatile NotificationFeed current = source is null
        ? new NotificationFeed { Entries = [], Reason = "no notification feed is configured on this Kit (KIT_AGENT_FEED is unset)" }
        : new NotificationFeed { Entries = [], Reason = "the notification feed has not been read yet" };

    private volatile AgentSchedule schedule = scheduleSource is null
        ? new AgentSchedule { Reason = "Kit is not told where the agent's schedule is (KIT_AGENT_SCHEDULE is unset)" }
        : new AgentSchedule { Reason = "the agent's schedule has not been read yet" };

    private string? etag;

    private string? scheduleEtag;

    /// <inheritdoc />
    public INotificationFeed Recent()
    {
        var feed = current;
        return new NotificationFeed { Entries = feed.Entries, Skipped = feed.Skipped, Reason = feed.Reason, Schedule = schedule };
    }

    /// <summary>Reads the feed and the schedule again. Never throws: a failed read keeps nothing and says why.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (files is null)
        {
            return;
        }

        if (scheduleSource is not null)
        {
            await RefreshScheduleAsync(files, scheduleSource, cancellationToken);
        }

        if (source is null)
        {
            return;
        }

        try
        {
            var read = await files.ReadFileAsync(source, etag, cancellationToken);
            if (read is not { } file)
            {
                return;
            }

            etag = file.ETag;
            current = Parse(file.Text);
        }
        catch (GitHubReadException e)
        {
            // A feed that cannot be read is said, never shown as an empty one. Entries read before
            // are dropped with it: an old list under a fresh-looking page would be the same lie.
            etag = null;
            var hint = e.Message.Contains("answered 404", StringComparison.Ordinal)
                ? " — if the repository is private, Kit's GitHub App may not have access to it"
                : string.Empty;
            current = new NotificationFeed { Entries = [], Reason = e.Message + hint };
        }
    }

    private async Task RefreshScheduleAsync(IGitHubFileReader reader, IProjectSource file, CancellationToken cancellationToken)
    {
        try
        {
            var read = await reader.ReadFileAsync(file, scheduleEtag, cancellationToken);
            if (read is not { } text)
            {
                return;
            }

            scheduleEtag = text.ETag;
            schedule = ParseSchedule(text.Text);
        }
        catch (GitHubReadException e)
        {
            // A schedule that cannot be read is said, never shown as an agent that is awake.
            scheduleEtag = null;
            schedule = new AgentSchedule { Reason = e.Message };
        }
    }

    /// <summary>
    /// The pause file as the agent's gate reads it (claude-code-bot <c>deploy/scripts/gate.sh</c>):
    /// paused only when <c>suspended</c> is true, and a pause whose <c>until</c> does not parse is no
    /// pause at all, because the gate runs a session then. Whether <c>until</c> has passed is the
    /// page's to say, since this is read once a poll and shown for longer.
    /// </summary>
    public static AgentSchedule ParseSchedule(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object)
            {
                return new AgentSchedule { Reason = "the agent's schedule file is not a JSON object" };
            }

            string? Str(string name) =>
                o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;

            // `jq -r` prints the boolean and the string alike, and the gate compares the printed text.
            var suspended = o.TryGetProperty("suspended", out var p)
                && (p.ValueKind == JsonValueKind.True || (p.ValueKind == JsonValueKind.String && p.GetString() == "true"));
            var until = Str("until");
            if (!suspended || (until is not null && !DateTimeOffset.TryParse(until, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _)))
            {
                return new AgentSchedule { Suspended = false };
            }

            return new AgentSchedule { Suspended = true, Until = until, ByAgent = Str("set_by") == "bot", Why = Str("reason") };
        }
        catch (JsonException)
        {
            return new AgentSchedule { Reason = "the agent's schedule file is not JSON" };
        }
    }

    /// <summary>
    /// The feed's lines, newest first and capped at <see cref="Cap"/>. A line that is not a
    /// notification is counted in <see cref="NotificationFeed.Skipped"/>, never fatal: one torn
    /// write must not hide every message after it. An unknown kind reads as <c>note</c>.
    /// </summary>
    public static NotificationFeed Parse(string text)
    {
        var entries = new List<Notification>();
        var skipped = 0;
        foreach (var line in text.Split('\n'))
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }

            if (Entry(line) is { } entry)
            {
                entries.Add(entry);
            }
            else
            {
                skipped++;
            }
        }

        // Appended oldest first, so the newest is last; the file order is the agent's, not the clock's.
        entries.Reverse();
        return new NotificationFeed { Entries = entries.Take(Cap).ToList(), Skipped = skipped };
    }

    private static Notification? Entry(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object
                || !o.TryGetProperty("at", out var at) || at.ValueKind != JsonValueKind.String
                || !o.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var kind = o.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String && Kinds.Contains(k.GetString()!)
                ? k.GetString()!
                : "note";
            return new Notification { At = at.GetString()!, Kind = kind, Text = text.GetString()! };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
