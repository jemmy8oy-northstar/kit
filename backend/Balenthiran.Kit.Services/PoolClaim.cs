namespace Balenthiran.Kit.Services;

/// <summary>
/// What a runner is handed by <c>POST /api/runs/claim</c>: the run, its token (shown to the runner once), and the work.
/// <paramref name="Stage"/> is the pipeline stage the run is for (BEH-ORCH-9; "test" is checked by the runner itself, BEH-ORCH-12); null for a run started by hand.
/// <paramref name="MaxSeconds"/> is the run's time limit (BEH-ORCH-17); null means the runner keeps its own.
/// </summary>
public sealed record PoolClaim(string Id, string Token, string Repo, string Task, string? BaseBranch, string Model, int? MaxTurns = null, string? Stage = null, int? MaxSeconds = null);
