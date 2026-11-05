namespace Balenthiran.Kit.Services;

/// <summary>A link's state and, when it is possibly broken, the plain sentence that says why.</summary>
public readonly record struct LinkVerdict(LinkState State, string? Reason = null);
