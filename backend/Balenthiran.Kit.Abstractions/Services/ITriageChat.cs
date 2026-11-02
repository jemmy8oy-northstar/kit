using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// BEH-SPECAGENT-4: the spec agent that talks an Inbox card through with him. It writes no code
/// and no spec: its reply is text he may copy into his answer. A fake stands in for the model in tests.
/// </summary>
public interface ITriageChat
{
    /// <summary>The model the replies come from, shown on the page so he knows what he is talking to.</summary>
    string Model { get; }

    /// <summary>
    /// One reply to <paramref name="turns"/>, given <paramref name="context"/> (the card and its project).
    /// Never throws for the model's refusal or an unreachable API: that is a <see cref="TriageReply"/> with no text.
    /// <paramref name="instructions"/> replaces the Inbox card's instructions, for the Chat page (BEH-CHAT-1).
    /// </summary>
    Task<TriageReply> ReplyAsync(string context, IReadOnlyList<TriageTurn> turns, string? instructions = null, CancellationToken cancellationToken = default);
}
