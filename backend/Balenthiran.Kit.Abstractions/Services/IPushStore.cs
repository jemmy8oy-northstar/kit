using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// BEH-NOTIFY-3: the phones that asked for pushes, and the high-water mark of the feed entries
/// already pushed. Both live together so a restart that forgets one cannot disagree with the other.
/// </summary>
public interface IPushStore
{
    /// <summary>Record a subscription, replacing the keys of one with the same endpoint. True when the endpoint was new.</summary>
    bool Add(IPushSubscription subscription);

    /// <summary>Forget the subscription with this endpoint. False when there was none.</summary>
    bool Remove(string endpoint);

    /// <summary>Every subscription.</summary>
    IReadOnlyList<IPushSubscription> All();

    /// <summary>The mark of the newest feed entry already pushed (or skipped at first start); null before the first start.</summary>
    string? Mark();

    /// <summary>
    /// Move the mark from <paramref name="expected"/> to <paramref name="next"/>, atomically. False, and
    /// nothing changed, when another tick moved it first — so two ticks never push the same entry.
    /// </summary>
    bool AdvanceMark(string? expected, string next);
}
