using System.Globalization;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="ICorpusHistory"/> over <c>git log</c> in the work tree the corpus sits in (BEH-BOARD-4).
/// </summary>
/// <remarks>
/// <para>
/// <c>--first-parent</c>, so a merge counts once, at the time it landed on this branch: on <c>dev</c>
/// a pull request's merge, on <c>kit/hosted</c> the edit's own commit. Without it a behaviour built
/// on a side branch would date from when it was written there, which is not when it was done here.
/// </para>
/// <para>
/// No <c>--follow</c>: a corpus renamed or moved starts its history at the rename, so every
/// behaviour already in it reads as added then.
/// </para>
/// <para>
/// ⚠️ A shallow clone (CI's checkout) has one commit, so every behaviour reads as added then and the
/// board falls back to corpus order among them. The pod's clone is full; nothing reads history in CI.
/// </para>
/// </remarks>
public sealed class GitCorpusHistory : ICorpusHistory
{
    /// <inheritdoc />
    public ICommitLog Log(string file)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(file))!;
        // A project read from GitHub that this server holds no clone of: an empty log here would
        // date every behaviour "newer than history", which is a claim, not an absence.
        if (!File.Exists(file))
        {
            return CommitLog.None("the corpus is not on this server's disk, so it has no history here");
        }

        if (GitStore.WorkTreeFor(file) is null)
        {
            return CommitLog.None("the corpus is not in a git work tree");
        }

        var r = GitStore.Git(["log", "--first-parent", "--format=%H %cI", "--", Path.GetFileName(file)], dir);
        if (!r.Ok)
        {
            return CommitLog.None(r.Failure!);
        }

        var commits = new List<ICommitStamp>();
        foreach (var line in r.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var space = line.IndexOf(' ');
            commits.Add(new CommitStamp
            {
                Commit = line[..space],
                At = DateTimeOffset.Parse(line[(space + 1)..], CultureInfo.InvariantCulture),
            });
        }

        commits.Reverse();
        return new CommitLog { Reason = null, Commits = commits };
    }

    /// <inheritdoc />
    public string? Text(string file, string commit)
    {
        // `<rev>:./<name>` is relative to the cwd, so the repository root never has to be known.
        var r = GitStore.Git(["show", $"{commit}:./{Path.GetFileName(file)}"], Path.GetDirectoryName(Path.GetFullPath(file))!);
        return r.Ok ? r.Stdout : null;
    }
}
