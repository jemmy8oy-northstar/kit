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
public sealed class NotificationReader(IGitHubFileReader? files, IProjectSource? source) : INotificationReader
{
    /// <summary>The most entries the page shows.</summary>
    public const int Cap = 50;

    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal) { "wake", "sleep", "pr", "blocker", "done", "note" };

    private volatile NotificationFeed current = source is null
        ? new NotificationFeed { Entries = [], Reason = "no notification feed is configured on this Kit (KIT_AGENT_FEED is unset)" }
        : new NotificationFeed { Entries = [], Reason = "the notification feed has not been read yet" };

    private string? etag;

    /// <inheritdoc />
    public INotificationFeed Recent() => current;

    /// <summary>Reads the feed again. Never throws: a failed read keeps no entries and says why.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (source is null || files is null)
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
