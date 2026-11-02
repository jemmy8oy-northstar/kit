namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>What the model answered: the text, or why there is none.</summary>
/// <param name="Text">The reply, or null when there is none.</param>
/// <param name="Failure">Why there is no reply, in a sentence; null when there is one.</param>
public sealed record TriageReply(string? Text, string? Failure = null);
