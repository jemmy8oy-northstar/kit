using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>What a review decided about a flagged bundle (kit-ui.beh <c>BEH-ORCH-2</c>).</summary>
public enum ReviewOutcome
{
    /// <summary>The bundle stands as the earlier stage wrote it.</summary>
    Agreed,

    /// <summary>The reviewer corrected Cards, Files or Context in place.</summary>
    Revised,

    /// <summary>It needs a decision from a person; the note says which.</summary>
    Escalated,
}

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-2</c>: the task text of the review stage, a run on a stronger model that checks a
/// flagged <see cref="ContextBundle"/> from the bundle alone, without searching again. Pure: nothing here starts
/// a run. The merged bundles are the record of every flag and its outcome; <see cref="Tally"/> counts them.
/// </summary>
public static class ReviewStage
{
    /// <summary>The task text for a review run of <paramref name="bundle"/>. THROWS for a bundle with no flag or one already reviewed.</summary>
    public static string BuildTask(string project, ContextBundle bundle)
    {
        if (bundle.Flag is null) throw new InvalidOperationException("only a flagged bundle is reviewed");
        if (bundle.Review is not null) throw new InvalidOperationException("this bundle has already been reviewed");

        var path = TriageStage.BundlePath(bundle.Slug);
        var sb = new StringBuilder();
        sb.Append("You are the review stage for project \"").Append(project).Append("\". An earlier stage flagged the bundle below with the reason \"")
            .Append(bundle.Flag).Append("\". Your job is to check it, not to build anything.\n\n");
        sb.Append("Steps:\n");
        sb.Append("1. Read the bundle below and ONLY the files it lists under Files. Do NOT search the repo beyond them: reading is cheaper than writing.\n");
        sb.Append("2. Decide one outcome: agreed (the bundle stands), revised (correct Cards, Files or Context in place), or escalated (it needs a decision from a person; say what decision in the note).\n");
        sb.Append("3. Keep the Flag line and add one line straight after it: \"Review: agreed|revised|escalated — <one-line note>\" (lower-case outcome, then a space, an em dash and a space, then the note).\n");
        sb.Append("4. Edit ").Append(path).Append(" on branch ").Append(TriageStage.Branch(bundle.Slug))
            .Append(". Change nothing but that bundle.\n");
        sb.Append("5. Commit and push the branch to origin, then STOP. Do not start any other work and do not open a pull request.\n\n");
        var rendered = bundle.Render();
        if (sb.Length + path.Length + rendered.Length + 40 <= KitRouter.MaxTask)
        {
            sb.Append("## Bundle (").Append(path).Append(")\n\n```\n").Append(rendered).Append("```\n");
        }
        else
        {
            sb.Append("## Bundle\n\nThe bundle is too long to carry here: read it at ").Append(path).Append(" on that branch first.\n");
        }

        return sb.ToString();
    }

    /// <summary>Counts the flagged bundles by review outcome; bundles with no flag are ignored.</summary>
    public static ReviewTally Tally(IEnumerable<ContextBundle> bundles)
    {
        var flagged = bundles.Where(b => b.Flag is not null).ToList();
        int Of(ReviewOutcome o) => flagged.Count(b => b.Review?.Outcome == o);
        return new ReviewTally(
            flagged.Count,
            flagged.Count(b => b.Review is null),
            Of(ReviewOutcome.Agreed),
            Of(ReviewOutcome.Revised),
            Of(ReviewOutcome.Escalated));
    }
}
