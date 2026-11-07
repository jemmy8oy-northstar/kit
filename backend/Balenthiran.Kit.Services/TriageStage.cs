using System.Text;

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

    /// <summary>Where a work item's branch lives.</summary>
    public static string Branch(string slug) => $"kit/work/{slug}";

    /// <summary>Where a work item's bundle lives on its branch.</summary>
    public static string BundlePath(string slug) => $".kit/bundles/{slug}.md";

    /// <summary>
    /// The task for a triage run of <paramref name="project"/>: notes are (source ref, text) in the order to triage
    /// them, cards are (id, title). The first <see cref="TriageTask.NotesTaken"/> notes are in it; the rest wait for
    /// the next run. THROWS when not even the first note fits.
    /// </summary>
    public static TriageTask BuildTask(
        string project,
        IReadOnlyList<(string Ref, string Text)> notes,
        IReadOnlyList<(string Id, string Title)> cards)
    {
        var sb = new StringBuilder();
        sb.Append("You are the triage stage for project \"").Append(project).Append("\". Your job is to collect context, not to write specs or code.\n\n");
        sb.Append("Steps:\n");
        sb.Append("1. Read the new notes below and the existing cards. Open a file only when a note needs it; read as little as you can.\n");
        sb.Append("2. For each note decide which existing cards it touches, or that a new card is needed. Do NOT write or edit any card, note or spec.\n");
        sb.Append("3. Group notes in the same domain into ONE work item. Give each work item a slug: lower-case words joined by hyphens, at most ")
            .Append(ContextBundle.MaxSlug).Append(" characters.\n");
        sb.Append("4. For each work item write one bundle at ").Append(BundlePath("<work-item-slug>"))
            .Append(" on branch ").Append(Branch("<work-item-slug>")).Append(". If that branch already exists on origin, check it out and update the bundle; otherwise create it from the branch you started on (not the default branch). Change nothing but the bundle.\n");
        sb.Append("5. Commit and push each branch to origin, then STOP. Do not start any other work and do not open a pull request.\n\n");
        sb.Append("A bundle has exactly this shape (sections in this order, every list item on its own \"- \" line):\n\n```\n");
        sb.Append("# <short title>\nWork-item: <work-item-slug>\nCards:\n- <existing card id such as BEH-ORCH-1, or \"new: <one line>\">\nNotes:\n- <source ref of each note it came from>\nFiles:\n- <repo path a builder will need>\nContext:\n<what a builder needs to know, so it need not search again>\n");
        sb.Append("```\n");
        sb.Append("If you are unsure of your assessment, add one line \"Flag: <one-line reason>\" straight after the Work-item line, so a stronger model reviews it. ").Append(FlagLevels.Guidance).Append(" Every note must appear in exactly one bundle.\n");
        sb.Append("A note that asks for nothing (a test, an empty or accidental note) goes in the work item \"").Append(SetAside)
            .Append("\" with a Flag line saying why and the one card line \"new: nothing to build\". It is not built: a person looks at it later.\n");
        sb.Append("A note that is an idea for later, not work to do now, goes in the work item \"").Append(Backlog)
            .Append("\" with a Flag line saying why and its card lines (existing ids, or \"new: <title>\"). It is not specced or built.\n\n");

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
            if (sb.Length + 40 + used + part.Length > KitRouter.MaxTask - Reserve)
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
}
