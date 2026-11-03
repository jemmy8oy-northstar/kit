namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>What the model answered: the text, or why there is none.</summary>
/// <param name="Text">The reply, or null when there is none.</param>
/// <param name="Failure">Why there is no reply, in a sentence; null when there is one.</param>
/// <param name="Action">What it asked to be done with the item in hand (BEH-CHAT-6), if anything.</param>
/// <param name="Options">The answers it proposes to a question it asked (BEH-CHAT-10), if any.</param>
public sealed record TriageReply(string? Text, string? Failure = null, TriageAction? Action = null, IReadOnlyList<string>? Options = null);
