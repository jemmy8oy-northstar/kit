using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// What the spec agent knows when it talks a card through (BEH-SPECAGENT-4): the card itself, as the
/// corpus writes it, and the project's outline — every epic, feature and behaviour by its title line.
/// The outline, not the whole corpus: kit-ui.beh is thousands of lines, and every turn re-sends this.
/// The Chat page (BEH-CHAT-1) reads the same two things, with the card optional and the other projects named.
/// </summary>
public static class TriageContext
{
    /// <summary>The longest outline sent, in characters; past it the outline says it was cut.</summary>
    public const int MaxOutline = 40_000;

    /// <summary>
    /// What the Chat page's agent is told to be (BEH-CHAT-1 to BEH-CHAT-4). It writes nothing: the page's
    /// own buttons answer an item or save a note, so the agent only talks and suggests.
    /// </summary>
    public const string ChatInstructions =
        "You are Kit's spec agent, in a chat with the owner about one of their projects at a time. You write no code. "
        + "When the owner has clearly decided the item in hand, do it with a tool rather than asking them to press a button: "
        + "approve_item, deny_item with what is true instead, or answer_item with their answer, in their words, and say in one "
        + "sentence what you did; the page shows it with an Undo. Call undo_last when they ask to undo. Never act on a guess: "
        + "if you are not sure they have decided, ask. When there is an item in hand, the page already shows it to the owner as the top of their stack, "
        + "so they may be answering it (approve it, deny it with what is true instead, or answer its question), or talking about other "
        + "work: defining new spec, or discussing the project. Infer which from what they say, and if they have moved on, do not press "
        + "them about the item. Be brief and plain: a few sentences, no headings. "
        + "Use the spec below, and if it does not say, say so rather than guess. When they define new spec, help them say it as one plain note: who does "
        + "what, and what they see. A line like [Now talking about x] means the chat moved to project x: the owner picked it on the page "
        + "or named it in their message. If what the owner says seems to be about a different project, ask whether it is, name the "
        + "projects from the list below that might fit, and ask them to name it in their next message or pick it on the page; if it "
        + "fits none, suggest leaving it as a note for a new project. A line like [In view now: id \"title\"] means that item came "
        + "into the chat at that point, and a line like [He approved id] says what the owner did with an item on the page; "
        + "do not ask them to do it again.";

    /// <summary>The card and its project's outline, or null when <paramref name="id"/> is not a behaviour in <paramref name="corpus"/>.</summary>
    public static string? Build(string app, string id, string corpus)
    {
        var lines = Lines(corpus);
        return Card(lines, id) is { } card
            ? $"Project: {app}\n\nThe card being decided:\n{card}\n\nThe project's spec, by title:\n{Outline(lines)}"
            : null;
    }

    /// <summary>
    /// The Chat page's context: the project's outline, the item in hand when there is one, and the other
    /// projects by name, so the agent can say when he seems to mean another. Null when <paramref name="id"/>
    /// is given and is not a behaviour in <paramref name="corpus"/>.
    /// </summary>
    public static string? BuildChat(string app, string? id, string corpus, IEnumerable<string> projects)
    {
        var lines = Lines(corpus);
        var others = string.Join(", ", projects.Where(p => p != app));
        var text = new StringBuilder($"Project: {app}\n\nThe other projects: {(others.Length > 0 ? others : "none")}\n\n");
        if (id is not null)
        {
            if (Card(lines, id) is not { } card)
            {
                return null;
            }

            text.Append($"The item in hand, waiting on the owner:\n{card}\n\n");
        }

        return text.Append($"The project's spec, by title:\n{Outline(lines)}").ToString();
    }

    private static string[] Lines(string corpus) => corpus.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static string? Card(string[] lines, string id)
    {
        var head = $"behaviour {id}";
        var start = Array.FindIndex(lines, l => l == head || l.StartsWith(head + " ", StringComparison.Ordinal));
        if (start < 0)
        {
            return null;
        }

        // A block is its head line and the indented lines under it; the next unindented line starts another.
        var end = start + 1;
        while (end < lines.Length && (lines[end].Length == 0 || char.IsWhiteSpace(lines[end][0])))
        {
            end++;
        }

        return string.Join('\n', lines[start..end]).TrimEnd();
    }

    private static string Outline(string[] lines)
    {
        var outline = new StringBuilder();
        foreach (var line in lines.Where(IsHead))
        {
            if (outline.Length + line.Length + 1 > MaxOutline)
            {
                outline.Append("… (the outline is cut here)\n");
                break;
            }

            outline.Append(line).Append('\n');
        }

        return outline.ToString();
    }

    private static bool IsHead(string line) =>
        line.StartsWith("epic ", StringComparison.Ordinal)
        || line.StartsWith("feature ", StringComparison.Ordinal)
        || line.StartsWith("behaviour ", StringComparison.Ordinal);
}
