using System.Globalization;
using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-13</c>: the task text of a scheduled review, a run on the review model (KIT_STAGE_MODEL_REVIEW) that
/// looks at the whole pipeline rather than one flagged item. It writes its findings as notes on the project and changes no code.
/// Pure: nothing here starts a run.
/// </summary>
public static class ReviewSweepStage
{
    /// <summary>The task for a scheduled review of <paramref name="project"/> on <paramref name="branch"/>, its notes in <paramref name="notesPath"/>, stamped at <paramref name="now"/>.</summary>
    public static string BuildTask(string project, string notesPath, string branch, DateTimeOffset now)
    {
        var stamp = now.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "Z";
        var sb = new StringBuilder();
        sb.Append("You are the review stage for project \"").Append(project).Append("\". This is a scheduled review of the whole pipeline, not of one flagged item. ")
            .Append("Your job is to check it, not to build anything.\n\n");
        sb.Append("Look at three things, reading the project's tickets and spec as they stand on branch ").Append(branch).Append(":\n");
        sb.Append("1. neglected tickets: tickets that have sat unmoved too long, are blocked with no answer, or were never picked up.\n");
        sb.Append("2. a sample of finished work: a few built or merged tickets, checked against the cards they say they build.\n");
        sb.Append("3. the architecture: whether the code and the spec still agree on how the parts fit, and anything drifting.\n\n");
        sb.Append("Steps:\n");
        sb.Append("1. Read what you need. Do not change anything yet.\n");
        sb.Append("2. Write each finding as a note: append it to ").Append(notesPath).Append(" on branch ").Append(branch)
            .Append(", under a heading line \"## ").Append(stamp).Append("\", then the finding in a few plain sentences. A check with no problem in it is not a note.\n");
        sb.Append("3. Change nothing but that notes file. Do not edit any code, spec or other file, and do not open a pull request.\n");
        sb.Append("4. Commit and push ").Append(branch).Append(" with that one file, then STOP.\n\n");
        sb.Append("Do not push any kit/work/ branch. Do not start any other work.\n");
        return sb.ToString();
    }
}
