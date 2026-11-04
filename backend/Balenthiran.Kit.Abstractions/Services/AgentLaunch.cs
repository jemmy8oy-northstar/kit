namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>What a launcher did with a run.</summary>
/// <param name="Started">True when a runner has taken the run.</param>
/// <param name="Note">A sentence saying what happened, shown to the operator.</param>
public readonly record struct AgentLaunch(bool Started, string Note);
