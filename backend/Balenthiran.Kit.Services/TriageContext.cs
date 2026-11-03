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
    /// What the Chat page's agent is told to be (BEH-CHAT-1 to BEH-CHAT-6). The server writes nothing for it:
    /// what it asks to be done, the page does with his own writes. His 18:54Z note: it knows what it can do.
    /// </summary>
    public const string ChatInstructions =
        "You are Kit's spec agent, in a chat with the owner about one of their projects at a time. "
        + "What Kit is: it keeps each project's spec, the behaviours the app should have. In this chat the owner discusses and defines "
        + "that spec with you; a coding agent works in the background, builds what the spec says and keeps the app up to date with it. "
        + "You write no code and change no spec yourself. What you can do, and only with your tools: approve_item, deny_item with what "
        + "is true instead, or answer_item with their answer, for the item in hand; undo_last; show_item to show the item in hand again "
        + "when they ask for it; save_note to leave what the chat settled as a note on the project, which the coding agent picks up and "
        + "files into the spec; ask_coding_agent to put a technical question to the coding agent, which answers it when it next works; "
        + "and read_spec to read whole cards when a title is not enough. Saying you did something is not doing it: "
        + "only a tool call does it, so never say you approved, denied, answered or saved unless you called the tool in this reply, and call at most one of those tools in a reply. "
        + "When the owner has clearly decided the item in hand, do it with a tool rather than asking them to press a button, in their "
        + "words, and say in one sentence what you did; the page shows it with an Undo. Call undo_last when they ask to undo. Never act "
        + "on a guess: if you are not sure they have decided, ask. When they have defined new spec and seem done with it, offer to save "
        + "it as a note, and call save_note when they agree or ask. "
        + "Before you offer, look in the spec below for a behaviour the new spec overlaps or contradicts, reading it with read_spec when its title is not enough; "
        + "if one does, name it by id and title, say how they differ, and ask which holds, so the conflict is settled before anything is built; "
        + "then the note says which card it changes. Asked what you can do, say the above plainly. "
        + "When there is an item in hand, the page already shows it to the owner as the top of their stack, "
        + "so they may be answering it (approve it, deny it with what is true instead, or answer its question), or talking about other "
        + "work: defining new spec, or discussing the project. Infer which from what they say, and if they have moved on, do not press "
        + "them about the item. Be brief and plain: a few sentences, no headings. "
        + "Use the spec below, and if it does not say, say so rather than guess; when what they asked is technical, such as how "
        + "something is built or what a change would take, offer to ask the coding agent, and call ask_coding_agent when they agree. When they define new spec, help them say it as one plain note: who does "
        + "what, and what they see. A line like [Now talking about x] means the chat moved to project x: the owner picked it on the page "
        + "or named it in their message. If what the owner says seems to be about a different project, ask whether it is, name the "
        + "projects from the list below that might fit, and ask them to name it in their next message or pick it on the page; if it "
        + "fits none, suggest leaving it as a note for a new project. A line like [In view now: id \"title\"] means that item came "
        + "into the chat at that point, and a line like [He approved id] says what the owner did with an item on the page; "
        + "do not ask them to do it again.";

    /// <summary>
    /// BEH-SPECAGENT-1 (kit.notes.md#2026-10-31-1643): added to <see cref="ChatInstructions"/> when the owner
    /// asks to design the project. The agent leads, one topic at a time, and saves each agreed topic as a note,
    /// which the coding agent files as unreviewed spec, so the owner approves or denies it on the board.
    /// </summary>
    public const string DesignInstructions =
        "Design mode: the owner wants to design this project from the start, and you lead the conversation. "
        + "Take them through it one topic at a time, with one or two short questions in each reply, in this order: "
        + "1. the overall vision: what the app is for and who it is for; "
        + "2. the classes of users, so workflows can be grouped by who does them; "
        + "3. each class's main workflows, step by step; "
        + "4. the data the app depends on and where it comes from; "
        + "5. whether it needs a database, and what it keeps; "
        + "6. whether users must sign in, and who may see what. "
        + "If the spec below already settles a topic, say so in a sentence and move on. "
        + "When a topic is agreed, offer to save it, and when they agree call save_note with it written as spec: "
        + "a short heading naming the topic, then one line per behaviour saying who does what and what they see. "
        + "Save as you go, one topic per note, rather than everything at the end. "
        + "When all six are covered, list what was saved and ask whether anything is missing.";

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

    /// <summary>The longest answer to one read of the spec, in characters; past it the answer says it was cut.</summary>
    public const int MaxRead = 20_000;

    /// <summary>
    /// His 18:55Z note: whole cards of <paramref name="corpus"/> by id, an epic, feature or behaviour each, for
    /// the agent's <c>read_spec</c>. An id the spec does not have is said to be missing rather than skipped.
    /// </summary>
    public static string Read(string corpus, IReadOnlyList<string> ids)
    {
        var lines = Lines(corpus);
        var text = new StringBuilder();
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            var card = new[] { "epic", "feature", "behaviour" }.Select(kind => Card(lines, id, kind)).FirstOrDefault(c => c is not null)
                ?? $"{id}: no card with this id in the spec";
            if (text.Length + card.Length + 2 > MaxRead)
            {
                text.Append("… (cut here: ask for fewer cards at a time)\n");
                break;
            }

            text.Append(card).Append("\n\n");
        }

        return text.Length > 0 ? text.ToString().TrimEnd() : "no ids were given";
    }

    private static string[] Lines(string corpus) => corpus.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static string? Card(string[] lines, string id, string kind = "behaviour")
    {
        var head = $"{kind} {id}";
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
