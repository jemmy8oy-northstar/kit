using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-NOTIFY-3: one tick of the push poller. Reads the feed, and pushes each entry newer than the
/// stored mark to every subscribed phone, once.
///
/// The first tick after a deploy sets the mark to the feed's newest entry WITHOUT pushing, so a
/// deploy never replays history. The mark is moved BEFORE anything is sent, by compare-and-swap in
/// the store: a push that then fails is lost rather than repeated (a phone buzzing twice for one
/// message is worse than missing one), and two ticks racing for the same entries — Kestrel is
/// concurrent, and a second replica would be too — cannot both win.
/// </summary>
/// <param name="reader">The feed; refreshed on every tick, since no one need have the page open.</param>
/// <param name="store">The subscriptions and the mark.</param>
/// <param name="sender">The push service; a fake in tests.</param>
/// <param name="basePath">Kit's path, so a tap on the notification opens <c>{basePath}/notifications</c>.</param>
public sealed class PushNotifier(INotificationReader reader, IPushStore store, IPushSender sender, string basePath)
{
    /// <summary>The longest body pushed; a phone shows about this much, and a push service caps the payload.</summary>
    public const int BodyLength = 180;

    private readonly SemaphoreSlim tick = new(1, 1);

    /// <summary>Reads the feed and pushes what is new. Never throws for a send; a feed that cannot be read pushes nothing and moves nothing.</summary>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        await tick.WaitAsync(cancellationToken);
        try
        {
            await reader.RefreshAsync(cancellationToken);
            var feed = reader.Recent();
            if (feed.Reason is not null)
            {
                // Could not look: neither start from this nor push from it.
                return;
            }

            var entries = feed.Entries;
            var newest = entries.Count > 0 ? Key(entries[0]) : string.Empty;
            var mark = store.Mark();
            if (mark is null)
            {
                store.AdvanceMark(null, newest);
                return;
            }

            var fresh = Fresh(entries, mark);
            if (fresh.Count == 0 || !store.AdvanceMark(mark, newest))
            {
                return;
            }

            // Oldest first, so the phone's stack reads in the order things happened.
            for (var i = fresh.Count - 1; i >= 0; i--)
            {
                await PushAsync(fresh[i], cancellationToken);
            }
        }
        finally
        {
            tick.Release();
        }
    }

    /// <summary>The identity of an entry for the mark: its time, and a hash of what it said, so two entries in one second differ.</summary>
    public static string Key(INotification entry) =>
        entry.At + "|" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.Kind + "\n" + entry.Text)))[..16];

    /// <summary>The body pushed for a message: cut at <see cref="BodyLength"/> characters with an ellipsis, never in the middle of a character.</summary>
    public static string Body(string text)
    {
        if (text.Length <= BodyLength)
        {
            return text;
        }

        var cut = BodyLength - 1;
        if (char.IsLowSurrogate(text[cut]))
        {
            cut--;
        }

        return text[..cut] + "…";
    }

    /// <summary>
    /// The entries (newest first) above the marked one. When the marked entry is no longer in the
    /// list — it fell out of the newest fifty, or the file was rewritten — the mark's time decides.
    /// </summary>
    private static List<INotification> Fresh(IReadOnlyList<INotification> entries, string mark)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(Key(entries[i]), mark, StringComparison.Ordinal))
            {
                return entries.Take(i).ToList();
            }
        }

        var markAt = mark.Contains('|', StringComparison.Ordinal) ? mark[..mark.LastIndexOf('|')] : null;
        return markAt is null ? entries.ToList() : entries.Where(e => After(e.At, markAt)).ToList();
    }

    private static bool After(string at, string than) =>
        DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var a)
        && DateTimeOffset.TryParse(than, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var b)
            ? a > b
            : string.CompareOrdinal(at, than) > 0;

    private async Task PushAsync(INotification entry, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["title"] = "Kit",
            ["body"] = Body(entry.Text),
            ["url"] = basePath + "/notifications",
        });

        foreach (var subscription in store.All())
        {
            try
            {
                if (await sender.SendAsync(subscription, payload, cancellationToken) == PushOutcome.Gone)
                {
                    store.Remove(subscription.Endpoint);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // One phone's failure must not stop the others.
                await Console.Error.WriteLineAsync($"kit: could not push to one subscription — {e.GetType().Name}: {e.Message}");
            }
        }
    }
}
