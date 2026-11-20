namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// BEH-VIS-2: what each agent run (one session) cost, as the session cost record (BEH-COST-1) keeps it. The run page
/// only reads it and never writes a cost. Null when no cost is recorded for the run, which the page shows as unknown.
/// </summary>
public interface IStageCostRecord
{
    decimal? CostOf(string runId);
}
