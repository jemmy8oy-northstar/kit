using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <see cref="ISessionCostStore"/> for a Kit with no <c>KIT_DATABASE</c>. ⚠️ Records live in this process only, so a
/// restart forgets them; only the Postgres store is meant for the deployed Kit.
/// </summary>
public sealed class MemorySessionCostStore : ISessionCostStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, ISessionCost> byRun = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Record(ISessionCost cost)
    {
        lock (gate)
        {
            byRun.TryAdd(cost.RunId, cost);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ISessionCost> List()
    {
        lock (gate)
        {
            return byRun.Values.OrderBy(c => c.Date).ToList();
        }
    }
}
