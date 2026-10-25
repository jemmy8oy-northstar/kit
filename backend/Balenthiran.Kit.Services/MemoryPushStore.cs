using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <see cref="IPushStore"/> for a Kit with no <c>KIT_DATABASE</c>. ⚠️ Subscriptions and the mark live
/// in memory only, so a restart forgets every phone: he turns notifications on again from the page,
/// and the poller starts from the feed's newest entry without pushing. Acceptable because only the
/// Postgres store is meant for the deployed Kit, where a restart must not silence his phone.
/// </summary>
public sealed class MemoryPushStore : IPushStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, PushSubscription> subscriptions = new(StringComparer.Ordinal);
    private string? mark;

    /// <inheritdoc />
    public bool Add(IPushSubscription subscription)
    {
        lock (gate)
        {
            var isNew = !subscriptions.ContainsKey(subscription.Endpoint);
            subscriptions[subscription.Endpoint] = new PushSubscription { Endpoint = subscription.Endpoint, P256dh = subscription.P256dh, Auth = subscription.Auth };
            return isNew;
        }
    }

    /// <inheritdoc />
    public bool Remove(string endpoint)
    {
        lock (gate)
        {
            return subscriptions.Remove(endpoint);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IPushSubscription> All()
    {
        lock (gate)
        {
            return subscriptions.Values.ToList<IPushSubscription>();
        }
    }

    /// <inheritdoc />
    public string? Mark()
    {
        lock (gate)
        {
            return mark;
        }
    }

    /// <inheritdoc />
    public bool AdvanceMark(string? expected, string next)
    {
        lock (gate)
        {
            if (!string.Equals(mark, expected, StringComparison.Ordinal))
            {
                return false;
            }

            mark = next;
            return true;
        }
    }
}
