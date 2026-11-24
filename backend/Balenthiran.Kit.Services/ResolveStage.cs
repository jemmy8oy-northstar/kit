using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-11</c>, slice 7b: the task text of the conflict-resolution run the merge job starts when a
/// built item's pull request conflicts with its base. The run works on the pull request's own branch (the one the build
/// run pushed), merges the base into it, resolves each conflict keeping what both sides meant, runs the tests and pushes
/// to that same branch. It opens no pull request, touches no other branch and never force-pushes. Pure: nothing here starts a run.
/// </summary>
public static class ResolveStage
{
    /// <summary>The task for a resolve run of <paramref name="bundle"/>'s branch in <paramref name="project"/>, whose pull request goes into <paramref name="baseBranch"/>.</summary>
    public static string BuildTask(string project, ContextBundle bundle, string baseBranch)
    {
        var branch = TriageStage.Branch(bundle.Slug);
        var sb = new StringBuilder();
        sb.Append("You are the conflict-resolution stage for project \"").Append(project).Append("\". The pull request from branch ").Append(branch)
            .Append(" into ").Append(baseBranch).Append(" conflicts with ").Append(baseBranch).Append(". Your job is to bring the branch up to date with it.\n\n");
        sb.Append("Steps:\n");
        sb.Append("1. Check out branch ").Append(branch).Append(" from origin. It is the pull request's own branch, and the only branch you change.\n");
        sb.Append("2. Merge origin/").Append(baseBranch).Append(" into it (a merge, not a rebase). Git will stop at the conflicts.\n");
        sb.Append("3. Resolve each conflict keeping the intent of both sides: what the branch added and what ").Append(baseBranch)
            .Append(" changed must both still work. Read the bundle below to see what the branch is for. Change nothing the conflicts do not need.\n");
        sb.Append("4. Run `git grep -n -E '^(<<<<<<<|=======|>>>>>>>)( |$)'`: it must find nothing. Then run the project's whole test suite and make it pass.\n");
        sb.Append("5. Commit the merge and push it to ").Append(branch).Append(" on origin as a plain push, then STOP.\n\n");
        // claude-code-bot#151: nearly every conflict the pipeline met was two branches adding lines at the same place in the
        // same shared files, so the run is told what each of those files is rather than left to work it out.
        sb.Append("How to resolve each kind of file (list them with `git diff --name-only --diff-filter=U`):\n");
        sb.Append("- .kit/memory.md is a log of notes: keep every line from both sides and drop nothing.\n");
        sb.Append("- A .beh spec: usually each side added its own behaviour blocks or contract lines at the same place. Keep both, whole, ")
            .Append(baseBranch).Append("'s first. If both sides gave a new behaviour the same id, keep ").Append(baseBranch)
            .Append("'s id and give the branch's behaviour the next free one, changing it everywhere the branch used it (tests, .tests.json, bundle). If both sides changed the same existing line of a behaviour differently, do not pick one: push nothing and say the run failed, naming that behaviour.\n");
        sb.Append("- A .tests.json map: keep every key from both sides, and make sure the file still parses as JSON (no doubled or missing commas).\n");
        sb.Append("- Code and tests: keep both sides' changes working together; the test suite decides.\n\n");
        sb.Append("Do not open a pull request, do not touch any other branch, and never force-push. If you cannot resolve a conflict so that the tests pass, push nothing and say the run failed.\n\n");
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
