using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-12</c>: the task text of the test run the merge job starts for a private project's pull request,
/// whose repo has no Actions CI to wait for. The run checks out exactly the pull request's head commit, runs the project's
/// whole test suite there and reports pass or fail; it changes, commits and pushes nothing and opens no pull request.
/// Pure: nothing here starts a run.
/// </summary>
public static class TestStage
{
    /// <summary>The task for a test run of <paramref name="bundle"/>'s branch at commit <paramref name="sha"/> in <paramref name="project"/>, whose pull request goes into <paramref name="baseBranch"/>.</summary>
    public static string BuildTask(string project, ContextBundle bundle, string baseBranch, string sha)
    {
        var branch = TriageStage.Branch(bundle.Slug);
        var sb = new StringBuilder();
        sb.Append("You are the test stage for project \"").Append(project).Append("\". The pull request from branch ").Append(branch)
            .Append(" into ").Append(baseBranch).Append(" has no CI to check it, so you run the project's tests at its head commit ").Append(sha).Append(".\n\n");
        sb.Append("Steps:\n");
        sb.Append("1. Fetch branch ").Append(branch).Append(" from origin and check out exactly commit ").Append(sha).Append(" (a detached checkout). Test that commit and nothing else.\n");
        sb.Append("2. Install what the project needs and run its whole test suite, every test project and every test step it has, not a subset.\n");
        sb.Append("3. Report the result: the run succeeds only if the whole suite passed at that commit. If any test fails, or the suite cannot be run, the run has failed: say which test or step and why.\n\n");
        sb.Append("Change nothing: do not edit, commit or push anything, do not open a pull request, and do not fix a failing test. STOP when the suite has finished.\n\n");
        var path = TriageStage.BundlePath(bundle.Slug);
        var rendered = bundle.Render();
        if (sb.Length + path.Length + rendered.Length + 40 <= KitRouter.MaxTask)
        {
            sb.Append("## Bundle (").Append(path).Append(")\n\n```\n").Append(rendered).Append("```\n");
        }
        else
        {
            sb.Append("## Bundle\n\nThe bundle is too long to carry here: read it at ").Append(path).Append(" on that branch.\n");
        }

        return sb.ToString();
    }
}
