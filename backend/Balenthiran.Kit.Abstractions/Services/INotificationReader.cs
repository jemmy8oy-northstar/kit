using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>BEH-NOTIFY-1: the agent's notifications, as last read.</summary>
public interface INotificationReader
{
    /// <summary>The body of <c>GET /api/notifications</c>. Never throws: an unreadable feed is a <see cref="DataModels.INotificationFeed.Reason"/>.</summary>
    INotificationFeed Recent();
}
