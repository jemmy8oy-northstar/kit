using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>BEH-VIS-2's cost seam: no session cost record exists yet (BEH-COST-1 not landed), so every cost is unknown.</summary>
public sealed class NoStageCostRecord : IStageCostRecord
{
    public static readonly NoStageCostRecord Instance = new();

    public decimal? CostOf(string runId) => null;
}
