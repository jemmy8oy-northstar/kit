namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// BEH-CHAT-6 (his 2026-11-02 17:53Z note): something the Chat page's agent asks to be done with the item
/// in hand, rather than only suggesting he press a button. The server writes nothing for it: the page
/// carries it out through the same operator-only writes as its buttons, and offers to undo it.
/// </summary>
/// <param name="Kind"><c>approve</c>, <c>deny</c> (with what is true instead), <c>answer</c> (with the answer), <c>undo</c> (the last one),
/// <c>show</c> (the item in hand, again) or <c>note</c> (what the chat agreed, left as a note on the project: his 19:04Z note).</param>
/// <param name="Text">The correction, the answer or the note, in his words; null for <c>approve</c>, <c>undo</c> and <c>show</c>.</param>
public sealed record TriageAction(string Kind, string? Text = null)
{
    /// <summary>The kinds the agent may ask for.</summary>
    public static readonly IReadOnlySet<string> Kinds = new HashSet<string>(StringComparer.Ordinal) { "approve", "deny", "answer", "undo", "show", "note" };

    /// <summary>Whether it acts on the item in hand, so means nothing when there is none. A method, so it is not serialised.</summary>
    public bool ActsOnItem() => Kind is "approve" or "deny" or "answer" or "show";
}
