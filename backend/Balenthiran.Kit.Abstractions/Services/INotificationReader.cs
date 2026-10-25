using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>BEH-NOTIFY-1: the agent's notifications, as last read.</summary>
public interface INotificationReader
{
    /// <summary>The body of <c>GET /api/notifications</c>. Never throws: an unreadable feed is a <see cref="DataModels.INotificationFeed.Reason"/>.</summary>
    INotificationFeed Recent();

    /// <summary>Reads the feed again, so the push poller sees an entry within a poll even when no one has the page open. Never throws.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
