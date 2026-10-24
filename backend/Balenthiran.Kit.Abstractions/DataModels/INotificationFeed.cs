namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// The body of <c>GET /api/notifications</c> (BEH-NOTIFY-1): the agent's messages, newest first.
/// They replace its Telegram messages (claude-code-bot#118), and are read from a file the agent
/// appends to in its own repository, so Kit stores nothing and the agent never writes to Kit.
/// </summary>
public interface INotificationFeed
{
    /// <summary>Newest first, capped.</summary>
    IReadOnlyList<INotification> Entries { get; }

    /// <summary>Lines that were not a notification, skipped rather than failing the whole feed.</summary>
    int Skipped { get; }

    /// <summary>
    /// Why the feed could not be read, or that none is configured. Null when it was read — even
    /// when it holds nothing. A feed Kit cannot read is never shown as an empty one.
    /// </summary>
    string? Reason { get; }
}
