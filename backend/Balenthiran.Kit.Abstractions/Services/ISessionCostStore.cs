using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// BEH-COST-1: where each Kit session's cost is kept, in Kit's database beside the per-user store, never in a repo.
/// </summary>
public interface ISessionCostStore
{
    /// <summary>
    /// Keeps a session's cost. The first record for a run stands: a report sent twice is counted once.
    /// </summary>
    void Record(ISessionCost cost);

    /// <summary>Every recorded session cost, oldest first.</summary>
    IReadOnlyList<ISessionCost> List();
}
