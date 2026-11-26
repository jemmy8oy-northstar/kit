using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-12</c>, claude-code-bot#151: the task text of the fix run the merge job starts when a built item's
/// test run failed at its pull request's head. As a conflict goes to a resolve run rather than stopping its ticket, a red
/// head goes to one run that makes the whole suite pass on the pull request's own branch. It opens no pull request, touches
/// no other branch and never force-pushes. Pure: nothing here starts a run.
/// </summary>
public static class FixStage
{
    /// <summary>The task for a fix run of <paramref name="bundle"/>'s branch in <paramref name="project"/>, whose test run failed at <paramref name="sha"/>; its pull request goes into <paramref name="baseBranch"/>.</summary>
    public static string BuildTask(string project, ContextBundle bundle, string baseBranch, string sha)
    {
        var branch = TriageStage.Branch(bundle.Slug);
        var sb = new StringBuilder();
        sb.Append("You are the fix stage for project \"").Append(project).Append("\". The pull request from branch ").Append(branch)
            .Append(" into ").Append(baseBranch).Append(" failed its test run at commit ").Append(sha)
            .Append(". Your job is to make it pass without changing what the branch is for.\n\n");
        sb.Append("Steps:\n");
        sb.Append("1. Check out branch ").Append(branch).Append(" from origin. It is the pull request's own branch, and the only branch you change.\n");
        sb.Append("2. Run ").Append(TestStage.WholeSuite).Append(". Note every step that fails.\n");
        sb.Append("3. Fix each failure the smallest way that keeps the bundle's intent: a missing test for a card the branch added, a test renamed without its .tests.json entry, a `pending` line left on a card that is now built, a card the branch added but did not build (give it a `pending` line), a card the branch added that repeats one already in the spec (delete it and name the existing card in the bundle). ")
            .Append("Do not delete or weaken a test, and do not mark pending a card that is built on ").Append(baseBranch).Append(", to make a step pass.\n");
        sb.Append("4. Run the whole suite again: it must pass.\n");
        sb.Append("5. Commit and push to ").Append(branch).Append(" on origin as a plain push, then STOP.\n\n");
        sb.Append("Do not open a pull request, do not touch any other branch, and never force-push. If you cannot make the suite pass, push nothing and say the run failed, naming the step.\n\n");
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
