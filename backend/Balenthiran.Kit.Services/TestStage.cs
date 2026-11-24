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
    /// <summary>
    /// claude-code-bot#151: what "the project's whole test suite" means, the same words in every stage that runs it (build,
    /// resolve, fix and test). The build stage once ran only the test projects and the test stage ran every CI step, so five
    /// pull requests passed their build, failed the spec gate at merge, and stopped.
    /// </summary>
    public const string WholeSuite = "the project's whole test suite: every test project, and every step its CI workflows under .github/workflows run "
        + "(build, lint, and any spec gate such as a `check` command), not a subset";


    /// <summary>The task for a test run of <paramref name="bundle"/>'s branch at commit <paramref name="sha"/> in <paramref name="project"/>, whose pull request goes into <paramref name="baseBranch"/>. A merge train (BEH-ORCH-18) names its own <paramref name="train"/> branch and the pull requests riding it.</summary>
    public static string BuildTask(string project, ContextBundle bundle, string baseBranch, string sha, (string Branch, IReadOnlyList<string> Riders)? train = null)
    {
        var branch = train?.Branch ?? TriageStage.Branch(bundle.Slug);
        var sb = new StringBuilder();
        sb.Append("You are the test stage for project \"").Append(project).Append("\". The pull request from branch ").Append(branch)
            .Append(" into ").Append(baseBranch).Append(" has no CI to check it, so you run the project's tests at its head commit ").Append(sha).Append(".\n\n");
        if (train is { } t)
        {
            sb.Append("This branch is a merge train: ").Append(baseBranch).Append(" with these pull requests merged onto it, tested together once: ").AppendJoin(", ", t.Riders).Append(".\n\n");
        }

        sb.Append("Steps:\n");
        sb.Append("1. Fetch branch ").Append(branch).Append(" from origin and check out exactly commit ").Append(sha).Append(" (a detached checkout). Test that commit and nothing else.\n");
        sb.Append("2. Install what the project needs and run ").Append(WholeSuite).Append(".\n");
        sb.Append("3. Report the result: the run succeeds only if the whole suite passed at that commit. If any test fails, or the suite cannot be run, the run has failed: say which test or step and why.\n");
        sb.Append("4. Leave the working directory checked out at commit ").Append(sha).Append(" when you finish: the runner checks HEAD.\n\n");
        sb.Append("Change nothing: do not edit, commit or push anything, do not open a pull request, and do not fix a failing test. STOP when the suite has finished.\n\n");
        sb.Append("Your final message must END with exactly one of these two lines, on its own line with nothing after it: `KIT-TEST: PASS ").Append(sha)
            .Append("` (the full commit hash) only if the whole suite passed at that commit, and otherwise `KIT-TEST: FAIL`. The runner reads that line, not your prose, and treats anything else as a failure.\n\n");
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
