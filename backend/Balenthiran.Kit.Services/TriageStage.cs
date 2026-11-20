using System.Text;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-1</c>: the task text of the triage stage, a short run on a cheap model that reads new
/// notes against the existing cards, writes a <see cref="ContextBundle"/> per work item on its own branch,
/// pushes, and ends. Pure: nothing here starts a run. The task embeds a capped amount of input so it stays small
/// enough for the cheap model's price tier; what is cut is said in the task, so the run knows to read the source.
/// The whole task always fits a run's <see cref="KitRouter.MaxTask"/>: notes that would not fit are left for a
/// later run (<see cref="TriageTask.NotesTaken"/>), and cards past the room left are counted, not listed.
/// </summary>
public static class TriageStage
{
    /// <summary>Most characters of one note's text the task carries.</summary>
    public const int MaxNoteChars = 1500;

    /// <summary>Most characters of note text, across all notes, the task carries.</summary>
    public const int MaxTotalNoteChars = 4000;

    /// <summary>Room kept back for the closing lines (the truncation note, the cards heading and the "more cards" line).</summary>
    private const int Reserve = 400;

    /// <summary>The work item for notes that ask for nothing (a test, an accidental note): it is never built, and stops for a person.</summary>
    public const string SetAside = "set-aside";

    /// <summary>The work item for notes that are ideas for later: it is never specced or built, and stops as "on the backlog".</summary>
    public const string Backlog = "backlog";

    /// <summary>BEH-ORCH-11: most open tickets one triage task lists; the highest priority first, newest first among equals.</summary>
    public const int MaxTickets = 50;

    /// <summary>BEH-ORCH-11: most characters the open-ticket list may take of the task, so it cannot crowd the notes out of the cheap model's input.</summary>
    public const int MaxTicketChars = 2500;

    /// <summary>BEH-ORCH-11: most characters of a ticket's title, and of each card it touches, in the list.</summary>
    public const int MaxTicketTitle = 60;

    private const int MaxTicketCards = 4;

    /// <summary>Where a work item's branch lives.</summary>
    public static string Branch(string slug) => $"kit/work/{slug}";

    /// <summary>Where a work item's bundle lives on its branch.</summary>
    public static string BundlePath(string slug) => $".kit/bundles/{slug}.md";

    /// <summary>
    /// A note's text as the triage reads it: one left on a card says which, on its own first line, so the text need not
    /// name the card it is about. A note on the project (null card) is its text unchanged.
    /// </summary>
    public static string OnCard(string? card, string text) =>
        card is null ? text : $"(Left on card {card}.)\n{text}";

    /// <summary>
    /// The task for a triage run of <paramref name="project"/>: notes are (source ref, text) in the order to triage
    /// them, cards are (id, title). The first <see cref="TriageTask.NotesTaken"/> notes are in it; the rest wait for
    /// the next run. THROWS when not even the first note fits.
    /// </summary>
    public static TriageTask BuildTask(
        string project,
        IReadOnlyList<(string Ref, string Text)> notes,
        IReadOnlyList<(string Id, string Title)> cards,
        IReadOnlyList<PipelineTicket>? tickets = null,
        string? specPath = null)
    {
        var ticketSection = TicketSection(tickets ?? []);
        var spec = specPath ?? "the project's spec file";
        var sb = new StringBuilder();
        sb.Append("You are the triage stage for project \"").Append(project).Append("\". Your job is to turn notes into a spec change and collect context, not to write tests or code.\n\n");
        sb.Append("Steps:\n");
        sb.Append("1. Read the new notes below and the existing cards. Open a file only when a note needs it; read as little as you can.\n");
        sb.Append("2. For each note decide which existing cards it touches, or that a new card is needed. Do not edit any note.\n");
        sb.Append("3. Group notes in the same domain into ONE work item. Give each work item a slug: lower-case words joined by hyphens, at most ")
            .Append(ContextBundle.MaxSlug).Append(" characters.\n");
        sb.Append("4. For each work item, on branch ").Append(Branch("<work-item-slug>"))
            .Append(" (if it already exists on origin, check it out; otherwise create it from the branch you started on, not the default branch), change only the spec and the bundle:\n");
        // BEH-ORCH-16 slice 2: the first agent writes the spec change; the spec stage after it only plans the tests.
        sb.Append("   a. Edit the spec at ").Append(spec).Append(". For each existing card the notes touch, add or correct its contract lines so they say what the notes ask. For each new card, add a behaviour card shaped like the cards around it, with a new id in the same area and the line \"review unreviewed\". Start every contract line you write with \"contract my assumption:\". Change no other card. Delete no card: when a note says one should go, add to it \"contract my assumption: delete this card, because <reason>\".\n");
        sb.Append("   b. Write one bundle at ").Append(BundlePath("<work-item-slug>")).Append(", listing under Cards the ids of every card you added or changed.\n");
        if (ticketSection.Length > 0)
        {
            sb.Append("   When a note covers the same thing as an open ticket listed below, report a bundle with THAT ticket's slug (an update of it: keep what still holds, add what the note brings) and do NOT open a second work item for it. Use a new slug only for work no open ticket covers.\n");
        }

        sb.Append("5. Commit and push each branch to origin, then STOP. Do not start any other work and do not open a pull request.\n");
        sb.Append("   Your turns are few: do each work item's checkout, spec edit, bundle write, commit and push in as few shell commands as you can, and push each before reading for the next. An unpushed change is lost.\n\n");
        sb.Append("A bundle has exactly this shape (sections in this order, every list item on its own \"- \" line):\n\n```\n");
        sb.Append("# <short title>\nWork-item: <work-item-slug>\nCards:\n- <card id such as BEH-ORCH-1>\nNotes:\n- <source ref of each note it came from>\nFiles:\n- <repo path a builder will need>\nContext:\n<what a builder needs to know, so it need not search again>\n");
        sb.Append("```\n");
        sb.Append("If you are unsure of your assessment, add one line \"Flag: <one-line reason>\" straight after the Work-item line, so a stronger model reviews it. ").Append(FlagLevels.Guidance).Append(" Every note must appear in exactly one bundle.\n");
        sb.Append("A note that asks for nothing (a test, an empty or accidental note) goes in the work item \"").Append(SetAside)
            .Append("\" with a Flag line saying why and the one card line \"new: nothing to build\". It is not built and its spec is not edited: a person looks at it later.\n");
        sb.Append("A note that is an idea for later, not work to do now, goes in the work item \"").Append(Backlog)
            .Append("\" with a Flag line saying why and its card lines (existing ids, or \"new: <title>\"). Its spec is not edited and it is not built.\n\n");

        var noteParts = new List<string>();
        var budget = MaxTotalNoteChars;
        var cut = false;
        var used = 0;
        foreach (var (reference, text) in notes)
        {
            var allowed = Math.Min(MaxNoteChars, budget);
            var shown = text.Length <= allowed ? text : text[..allowed];
            var part = $"\n### {reference}\n" + (shown.Length < text.Length
                ? $"{shown}\n[truncated: {text.Length - shown.Length} more characters, read {reference} for the rest]\n"
                : $"{shown}\n");
            if (sb.Length + 40 + used + part.Length + ticketSection.Length > KitRouter.MaxTask - Reserve)
            {
                break;
            }

            cut |= shown.Length < text.Length;
            budget -= shown.Length;
            used += part.Length;
            noteParts.Add(part);
        }

        if (noteParts.Count == 0 && notes.Count > 0)
        {
            throw new InvalidOperationException($"note {notes[0].Ref} does not fit a triage task of {KitRouter.MaxTask} characters");
        }

        sb.Append("## New notes (").Append(noteParts.Count).Append(")\n");
        foreach (var part in noteParts)
        {
            sb.Append(part);
        }

        if (cut)
        {
            sb.Append("\nNote: some text above was TRUNCATED to keep this task small (").Append(MaxNoteChars)
                .Append(" characters a note, ").Append(MaxTotalNoteChars).Append(" in all). Read the source ref when a cut note matters.\n");
        }

        sb.Append(ticketSection);
        sb.Append("\n## Existing cards (").Append(cards.Count).Append(")\n");
        var listed = 0;
        foreach (var (id, title) in cards)
        {
            var line = $"- {id}: {title}\n";
            if (sb.Length + line.Length > KitRouter.MaxTask - 120)
            {
                break;
            }

            sb.Append(line);
            listed++;
        }

        if (listed < cards.Count)
        {
            sb.Append("[").Append(cards.Count - listed).Append(" more cards not listed: read the project's spec files for them]\n");
        }

        return new TriageTask(sb.ToString(), noteParts.Count);
    }

    /// <summary>
    /// BEH-ORCH-11: the project's tickets not yet built (new, ready, planned) as "- slug [state] title (cards)", highest
    /// priority first and newest first among equals, at most <see cref="MaxTickets"/> lines and
    /// <see cref="MaxTicketChars"/> characters; the rest are counted. Empty when there is no open ticket.
    /// <paramref name="tickets"/> are in the store's order, oldest first. A built ticket is not listed: an update to it
    /// would send its item back to spec and leave its pull request behind, so follow-up work on it is a new ticket.
    /// </summary>
    public static string TicketSection(IReadOnlyList<PipelineTicket> tickets)
    {
        var open = tickets
            .Select((t, i) => (Ticket: t, Order: i))
            .Where(x => x.Ticket.State is TicketState.New or TicketState.Ready or TicketState.Planned && x.Ticket.BlockedBecause is null)
            .OrderBy(x => x.Ticket.Priority)
            .ThenByDescending(x => x.Order)
            .Select(x => x.Ticket)
            .ToList();
        if (open.Count == 0)
        {
            return "";
        }

        var head = $"\n## Open tickets ({open.Count})\n";
        var sb = new StringBuilder(head);
        var listed = 0;
        foreach (var t in open.Take(MaxTickets))
        {
            var cardList = string.Join(", ", t.Cards.Take(MaxTicketCards).Select(c => Short(c, MaxTicketTitle)));
            var more = t.Cards.Count > MaxTicketCards ? $" +{t.Cards.Count - MaxTicketCards} more" : "";
            var line = $"- {t.Slug} [{t.State.ToString().ToLowerInvariant()}] {Short(t.Title, MaxTicketTitle)} ({cardList}{more})\n";
            if (sb.Length + line.Length > MaxTicketChars - 80)
            {
                break;
            }

            sb.Append(line);
            listed++;
        }

        if (listed < open.Count)
        {
            sb.Append('[').Append(open.Count - listed).Append(" more open tickets not listed: a note about one of them may open a duplicate]\n");
        }

        return sb.ToString();
    }

    private static string Short(string text, int max)
    {
        var one = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return one.Length <= max ? one : one[..(max - 1)] + "…";
    }
}
