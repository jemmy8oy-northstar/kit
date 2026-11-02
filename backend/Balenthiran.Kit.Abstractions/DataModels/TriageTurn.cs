namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One message in a triage chat (BEH-SPECAGENT-4): his, or the spec agent's.</summary>
/// <param name="FromUser">True for his message, false for the agent's.</param>
/// <param name="Text">What was said.</param>
public sealed record TriageTurn(bool FromUser, string Text);
