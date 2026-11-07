using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-3</c>: the task text of the build stage, a run that starts from a specced
/// <see cref="ContextBundle"/>, writes the tests its test plan names first, then the code, until they and the
/// project's suite pass, says what it built in the bundle, pushes the work item's branch and ends. Kit opens the
/// pull request, not the run. Pure: nothing here starts a run.
/// </summary>
public static class BuildStage
{
    /// <summary>The heading the build stage writes what it built under, inside the bundle's Context.</summary>
    public const string BuiltHeading = "Built:";

    /// <summary>
    /// The task for a build run of <paramref name="bundle"/> in <paramref name="project"/>. The bundle is carried
    /// whole when it fits a run's task, and otherwise the run is told to read it first. THROWS for a bundle with no
    /// test plan: there is nothing to build from.
    /// </summary>
    public static string BuildTask(string project, ContextBundle bundle)
    {
        if (!bundle.Context.Contains(SpecStage.TestPlanHeading, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("the bundle has no test plan to build from");
        }

        var path = TriageStage.BundlePath(bundle.Slug);
        var branch = TriageStage.Branch(bundle.Slug);
        var sb = new StringBuilder();
        sb.Append("You are the build stage for project \"").Append(project).Append("\". Your job is to write the tests the bundle's test plan names, then the code that makes them pass.\n\n");
        sb.Append("Steps:\n");
        sb.Append("1. Check out branch ").Append(branch).Append(" from origin. Every change below goes on it.\n");
        sb.Append("2. Read the bundle below, the spec cards it names under Cards and the files it lists under Files. Search beyond them only when the bundle is wrong.\n");
        sb.Append("3. Write the tests under \"").Append(SpecStage.TestPlanHeading).Append("\" first, each naming its card id the way the tests around it do. Run them and see them fail.\n");
        sb.Append("4. Write the code until those tests pass, then run the project's whole test suite and make it pass. Change nothing the cards do not ask for.\n");
        sb.Append("5. In ").Append(path).Append(", at the end of Context add a line \"").Append(BuiltHeading)
            .Append("\" and under it one \"- \" line per test you wrote, then one line saying how many tests the whole suite ran and that it passed.\n");
        sb.Append("6. If you are unsure of what you built and the bundle has no Flag line, add one line \"Flag: <one-line reason>\" straight after the Work-item line, so a stronger model reviews it. ").Append(FlagLevels.Guidance).Append('\n');
        sb.Append("7. Commit and push the branch to origin, then STOP. Do not open a pull request: Kit opens it.\n\n");
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

    /// <summary>The pull request's body: the bundle that led to it, so the reviewer reads what the run was given and what it says it built.</summary>
    public static string PullRequestBody(string project, ContextBundle bundle) =>
        $"Built by Kit's pipeline for project \"{project}\" from work item `{bundle.Slug}` (BEH-ORCH-3). Its bundle:\n\n````\n{bundle.Render()}````\n";
}
