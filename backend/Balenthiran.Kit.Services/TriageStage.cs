using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-1</c>: the task text of the triage stage, a short run on a cheap model that reads new
/// notes against the existing cards, writes a <see cref="ContextBundle"/> per work item on its own branch,
/// pushes, and ends. Pure: nothing here starts a run. The task embeds a capped amount of input so it stays small
/// enough for the cheap model's price tier; what is cut is said in the task, so the run knows to read the source.
/// </summary>
public static class TriageStage
{
    /// <summary>Most characters of one note's text the task carries.</summary>
    public const int MaxNoteChars = 2000;

    /// <summary>Most characters of note text, across all notes, the task carries.</summary>
    public const int MaxTotalNoteChars = 12000;

    /// <summary>Where a work item's branch lives.</summary>
    public static string Branch(string slug) => $"kit/work/{slug}";

    /// <summary>Where a work item's bundle lives on its branch.</summary>
    public static string BundlePath(string slug) => $".kit/bundles/{slug}.md";

    /// <summary>The task text for a triage run of <paramref name="project"/>: notes are (source ref, text), cards are (id, title).</summary>
    public static string BuildTask(
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
            .Append(" on branch ").Append(Branch("<work-item-slug>")).Append(". If that branch already exists on origin, check it out and update the bundle; otherwise create it from the default branch. Change nothing but the bundle.\n");
        sb.Append("5. Commit and push each branch to origin, then STOP. Do not start any other work and do not open a pull request.\n\n");
        sb.Append("A bundle has exactly this shape (sections in this order, every list item on its own \"- \" line):\n\n```\n");
        sb.Append("# <short title>\nWork-item: <work-item-slug>\nCards:\n- <existing card id such as BEH-ORCH-1, or \"new: <one line>\">\nNotes:\n- <source ref of each note it came from>\nFiles:\n- <repo path a builder will need>\nContext:\n<what a builder needs to know, so it need not search again>\n");
        sb.Append("```\n");
        sb.Append("If you are unsure of your assessment, add one line \"Flag: <one-line reason>\" straight after the Work-item line, so a stronger model reviews it. Every note must appear in exactly one bundle.\n\n");

        sb.Append("## New notes (").Append(notes.Count).Append(")\n");
        var budget = MaxTotalNoteChars;
        var cut = false;
        foreach (var (reference, text) in notes)
        {
            var allowed = Math.Min(MaxNoteChars, budget);
            var shown = text.Length <= allowed ? text : text[..allowed];
            budget -= shown.Length;
            sb.Append("\n### ").Append(reference).Append('\n');
            if (shown.Length < text.Length)
            {
                cut = true;
                sb.Append(shown).Append("\n[truncated: ").Append(text.Length - shown.Length).Append(" more characters, read ").Append(reference).Append(" for the rest]\n");
            }
            else
            {
                sb.Append(shown).Append('\n');
            }
        }

        if (cut)
        {
            sb.Append("\nNote: some text above was TRUNCATED to keep this task small (").Append(MaxNoteChars)
                .Append(" characters a note, ").Append(MaxTotalNoteChars).Append(" in all). Read the source ref when a cut note matters.\n");
        }

        sb.Append("\n## Existing cards (").Append(cards.Count).Append(")\n");
        foreach (var (id, title) in cards)
        {
            sb.Append("- ").Append(id).Append(": ").Append(title).Append('\n');
        }

        return sb.ToString();
    }
}
