namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>Token counts for one model call, by class (BEH-USERS-9).</summary>
public readonly record struct AgentUsage(long Input, long Output, long CacheWrite, long CacheRead);
