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
    public ICommitHeadlines Recent(string file, string notes, int limit)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(file))!;
        if (!File.Exists(file))
        {
            return CommitHeadlines.None("the corpus is not on this server's disk, so it has no history here");
        }

        if (GitStore.WorkTreeFor(file) is null)
        {
            return CommitHeadlines.None("the corpus is not in a git work tree");
        }

        string[] paths = [Path.GetFileName(file), Path.GetFileName(notes)];
        var top = Commits(dir, null, paths, limit);
        if (top.Failure is { } failure)
        {
            return CommitHeadlines.None(failure);
        }

        var commits = new List<ICommitHeadline>();
        foreach (var c in top.Commits)
        {
            // A merge that is not a pull request is a branch sync — on kit/hosted, "bring kit/hosted
            // up to date with dev" — and says nothing about what it brought. Show what it brought:
            // the merged side's own first-parent commits that touched these files, one level deep.
            if (c.Parents.Length == 2 && !PullRequestMerge.IsMatch(c.Subject))
            {
                // Not `log p1..p2 -- paths`: with a range, a merge whose first parent is excluded is
                // compared only to its other parent, which carries the same file, so git drops it —
                // every sync after the first came back empty (kit#352 live). Walk the merged side's
                // path-limited history from its tip instead, and keep what the range says is new.
                var brought = GitStore.Git(["rev-list", $"{c.Parents[0]}..{c.Parents[1]}"], dir);
                var side = Commits(dir, c.Parents[1], paths, limit);
                if (brought.Ok && side.Failure is null)
                {
                    var fresh = brought.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                    var shown = side.Commits.Where(s => fresh.Contains(s.Full)).Select(s => s.Headline).ToList();
                    if (shown.Count > 0)
                    {
                        commits.AddRange(shown);
                        continue;
                    }
                }
            }

            commits.Add(c.Headline);
        }

        return new CommitHeadlines { Reason = null, Commits = commits.Take(limit).ToList() };
    }

    private static readonly System.Text.RegularExpressions.Regex PullRequestMerge =
        new(@"^Merge pull request (#\d+) from \S*?([^/\s]+)\s*\z", System.Text.RegularExpressions.RegexOptions.Compiled);

    private sealed record Raw(string Full, string[] Parents, string Subject, ICommitHeadline Headline);

    private static (string? Failure, IReadOnlyList<Raw> Commits) Commits(string dir, string? from, string[] paths, int limit)
    {
        // Unit separator between fields, record separator between commits: a message can hold any
        // printable character and any newline, never these. The body is read for merges (Headline).
        List<string> args = ["log", "--first-parent", $"--max-count={limit}", "--format=%H%x1f%P%x1f%cI%x1f%s%x1f%b%x1e"];
        if (from is not null)
        {
            args.Add(from);
        }

        args.Add("--");
        args.AddRange(paths);
        var r = GitStore.Git([.. args], dir);
        if (!r.Ok)
        {
            return (r.Failure!, []);
        }

        var commits = new List<Raw>();
        foreach (var record in r.Stdout.Split('\u001e'))
        {
            var parts = record.Trim().Split('\u001f', 5);
            if (parts.Length != 5)
            {
                continue;
            }

            commits.Add(new Raw(
                parts[0],
                parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries),
                parts[3],
                new CommitHeadline
                {
                    Commit = parts[0][..Math.Min(7, parts[0].Length)],
                    At = DateTimeOffset.Parse(parts[2], CultureInfo.InvariantCulture),
                    Subject = Headline(parts[3], parts[4]),
                }));
        }

        return (null, commits);
    }

    /// <summary>
    /// What a reader should see for a commit. <c>--first-parent</c> makes every pull request on
    /// <c>dev</c> its merge commit, whose subject is "Merge pull request #N from owner/branch" — it
    /// names the branch, not the change. GitHub puts the PR's title on the body's first line, so that
    /// is the headline, with the number kept. A merge with no body falls back to its branch's last
    /// segment in words, which still says more than the owner does.
    /// </summary>
    internal static string Headline(string subject, string body)
    {
        var pr = PullRequestMerge.Match(subject);
        if (!pr.Success)
        {
            return subject;
        }

        var title = body.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0)
            ?? pr.Groups[2].Value.Replace('-', ' ');
        return $"{title} ({pr.Groups[1].Value})";
    }

    /// <inheritdoc />
    public string? Text(string file, string commit)
    {
        // `<rev>:./<name>` is relative to the cwd, so the repository root never has to be known.
        var r = GitStore.Git(["show", $"{commit}:./{Path.GetFileName(file)}"], Path.GetDirectoryName(Path.GetFullPath(file))!);
        return r.Ok ? r.Stdout : null;
    }
}
