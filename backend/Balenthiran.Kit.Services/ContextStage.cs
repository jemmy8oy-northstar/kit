using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-11</c>, slice 3: the task text of the context job's run, a short run on a cheap model that
/// starts from a triage's <see cref="ContextBundle"/>, reads the repo and the spec for the cards the bundle names,
/// adds to the bundle what a planner needs, pushes the work item's branch and ends. It writes no spec and no code:
/// planning is the spec stage and building the build stage. Pure: nothing here starts a run.
/// </summary>
public static class ContextStage
{
    /// <summary>
    /// The task for a context run of <paramref name="bundle"/> in <paramref name="project"/>, whose spec is at
    /// <paramref name="specPath"/> in the run's repo. The bundle is carried whole when it fits a run's task, and
    /// otherwise the run is told to read it first.
    /// </summary>
    public static string BuildTask(string project, string specPath, ContextBundle bundle)
    {
        var path = TriageStage.BundlePath(bundle.Slug);
        var branch = TriageStage.Branch(bundle.Slug);
        var sb = new StringBuilder();
        sb.Append("You are the context stage for project \"").Append(project).Append("\". Your job is to gather what a planner needs and add it to the bundle, not to write the spec, tests or code.\n\n");
        sb.Append("Steps:\n");
        sb.Append("1. Check out branch ").Append(branch).Append(" from origin. Every change below goes on it.\n");
        sb.Append("2. Read the bundle below, the spec at ").Append(specPath).Append(" for each card under Cards, and the files the bundle lists under Files. Search the repo for what else the work touches, and read as little as you can.\n");
        sb.Append("3. In ").Append(path).Append(", add to the bundle what a planner needs so that it need not search again: under Files the repo paths that matter, and at the end of Context short \"- \" lines for the existing behaviours and tests that cover this area, the constraints the code or the spec puts on the change, and the open questions a planner must settle. Keep what the bundle already says unless it is wrong. Change the Work-item line and the Notes list never.\n");
        sb.Append("4. Write no spec, no tests and no code, and edit nothing but the bundle.\n");
        sb.Append("5. If you are unsure of the bundle and it has no Flag line, add one line \"Flag: <one-line reason>\" straight after the Work-item line, so a stronger model reviews it. ").Append(FlagLevels.Guidance).Append('\n');
        sb.Append("6. Commit and push the branch to origin, then STOP. Do not start any other work and do not open a pull request. Report the bundle back under the same Work-item slug.\n\n");
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
}
