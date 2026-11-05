namespace Balenthiran.Kit.Services;

/// <summary>What a runner is handed by <c>POST /api/runs/claim</c>: the run, its token (shown to the runner once), and the work.</summary>
public sealed record PoolClaim(string Id, string Token, string Repo, string Task, string? BaseBranch, string Model);
