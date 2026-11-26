using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-3</c>: the task text of the spec stage, a short run on a cheap model that starts from a
/// triage's <see cref="ContextBundle"/>, reads the cards the triage wrote (BEH-ORCH-16), plans their tests in the bundle,
/// pushes the work item's branch and ends. It writes no tests or code:
/// that is the build stage. Pure: nothing here starts a run.
/// </summary>
public static class SpecStage
{
    /// <summary>The heading the spec stage writes its test plan under, inside the bundle's Context.</summary>
    public const string TestPlanHeading = "Test plan:";

    /// <summary>
    /// The task for a spec run of <paramref name="bundle"/> in <paramref name="project"/>, whose spec is at
    /// <paramref name="specPath"/> in the run's repo. The bundle is carried whole when it fits a run's task, and
    /// otherwise the run is told to read it first.
    /// </summary>
    public static string BuildTask(string project, string specPath, ContextBundle bundle, bool kitWrote = false)
    {
        var path = TriageStage.BundlePath(bundle.Slug);
        var branch = TriageStage.Branch(bundle.Slug);
        var sb = new StringBuilder();
        sb.Append("You are the spec stage for project \"").Append(project).Append("\". Your job is to write the spec and plan the tests, not to write tests or code.\n\n");
        sb.Append("Steps:\n");
        if (kitWrote)
        {
            // BEH-ORCH-4: no triage ran, so the branch and the bundle file do not exist yet.
            sb.Append("1. Check out branch ").Append(branch).Append(" from origin if it exists there; otherwise create it from the branch you started on. Every change below goes on it.\n");
            sb.Append("2. Write the bundle below, exactly as given, to ").Append(path).Append(" (create the directory), then read the files it lists under Files. Search beyond them only when the bundle is wrong.\n");
        }
        else
        {
            sb.Append("1. Check out branch ").Append(branch).Append(" from origin. Every change below goes on it.\n");
            sb.Append("2. Read the bundle below and the files it lists under Files. Search beyond them only when the bundle is wrong.\n");
        }

        // BEH-ORCH-16 slice 2: the triage already wrote the cards; this stage plans their tests. A bundle written before
        // that may still say "new:", so the old instruction stays for it.
        sb.Append("3. Read the cards under Cards in the spec at ").Append(specPath).Append(": they are already written. Do not change them; if one is wrong for its notes, say so in a Flag line (step 6). Only when a Cards line still says \"new: <one line>\", add that card shaped like the cards around it, with a new id in the same area, the lines \"review unreviewed\" and \"pending\" and contract lines starting \"contract my assumption:\", and replace the line in ")
            .Append(path).Append(" with its id.\n");
        sb.Append("4. In ").Append(path).Append(", at the end of Context add a line \"")
            .Append(TestPlanHeading).Append("\" and under it one \"- \" line per test: the card id, then what the test checks.\n");
        sb.Append("5. Write no tests and no code.\n");
        sb.Append("6. If you are unsure of the spec and the bundle has no Flag line, add one line \"Flag: <one-line reason>\" straight after the Work-item line, so a stronger model reviews it. ").Append(FlagLevels.Guidance).Append('\n');
        sb.Append("7. Commit and push the branch to origin, then STOP. Do not start any other work and do not open a pull request.\n\n");
        var rendered = bundle.Render();
        if (sb.Length + path.Length + rendered.Length + 40 <= KitRouter.MaxTask)
        {
            sb.Append("## Bundle (").Append(path).Append(")\n\n```\n").Append(rendered).Append("```\n");
        }
        else if (kitWrote)
        {
            // Not on any branch to read, so a bundle that cannot be carried cannot be used.
            throw new InvalidOperationException("the scheduled card's bundle is too long to carry in a run's task");
        }
        else
        {
            sb.Append("## Bundle\n\nThe bundle is too long to carry here: read it at ").Append(path).Append(" on that branch first.\n");
        }

        return sb.ToString();
    }
}
