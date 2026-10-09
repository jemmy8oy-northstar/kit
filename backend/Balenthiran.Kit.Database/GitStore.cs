using System.ComponentModel;
using System.Diagnostics;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <c>git-store.js</c>, scored against it by <c>conformance/routes/git.json</c>: a real bare
/// remote and a real clone per scenario, and what the REMOTE holds afterwards.
/// </summary>
/// <param name="enabled">Off unless asked for — local Kit keeps decision 2 exactly as it was.</param>
/// <param name="remote">Default <c>origin</c>.</param>
/// <param name="branch">Default: whatever is checked out. A detached HEAD is refused rather than guessed at.</param>
/// <param name="push">Commit only, without pushing.</param>
/// <param name="name">Commit identity, passed with <c>-c</c> rather than written into the clone's config.</param>
/// <param name="email">As <paramref name="name"/>.</param>
/// <param name="baseBranch">
/// The branch Commit proposes INTO (<c>KIT_GIT_BASE</c>). A write whose target is this branch is
/// refused before anything is committed — <c>kit-hosted.beh</c> BEH-COMMIT-1, "never a commit on
/// dev". Until this the only guard was the entrypoint checking out <c>kit/hosted</c>; a clone left on
/// <c>dev</c> with write-back on pushed every phone edit straight onto it. Null: no branch is refused.
/// </param>
public sealed class GitStore(
    bool enabled,
    string? remote = null,
    string? branch = null,
    bool push = true,
    string? name = null,
    string? email = null,
    string? baseBranch = null) : IGitStore
{
    /// <summary>Identity used when the pod has no git config of its own.</summary>
    public const string DefaultName = "kit";

    /// <inheritdoc cref="DefaultName"/>
    public const string DefaultEmail = "kit@users.noreply.github.com";

    /// <inheritdoc />
    public bool Enabled => enabled;

    /// <inheritdoc />
    public string Remote => remote ?? "origin";

    /// <summary><c>message()</c>: <c>kit: add BEH-7 (snip-it)</c> — readable in <c>git log</c> without the diff.</summary>
    public static string Message(string? summary, string? app)
    {
        var what = string.IsNullOrEmpty(summary) ? "update" : summary.Trim();
        return string.IsNullOrEmpty(app) ? $"kit: {what}" : $"kit: {what} ({app})";
    }

    /// <summary>
    /// Run one git command and say what happened to it. <see cref="Call.Failure"/> names the
    /// LAYER — git could not be run, or it exited non-zero — because the two need different fixes
    /// (kit#39). ⚠️ <c>git.js</c> has a third, "killed by SIGx"; .NET reports a signalled child
    /// as exit 128+n, indistinguishable from git's own exit 129, so here it reads as an exit.
    /// </summary>
    public static Call Git(IReadOnlyList<string> args, string cwd)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
        {
            start.ArgumentList.Add(a);
        }

        Process process;
        try
        {
            process = Process.Start(start)!;
        }
        catch (Win32Exception e)
        {
            // A missing binary or a missing directory: "git is not installed" must not read as "your push was rejected".
            return new Call(false, $"git could not be run ({Errno(e)})", string.Empty, string.Empty);
        }

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            var (o, err) = (stdout.Result, stderr.Result);
            if (process.ExitCode != 0)
            {
                var first = ReasonLine(err);
                return new Call(false, $"git {args[0]} exited {process.ExitCode}: {(first.Length > 0 ? first : $"exit {process.ExitCode}")}", o, err);
            }

            return new Call(true, null, o, err);
        }
    }

    /// <summary>
    /// The line of git's stderr that says WHY: the first marked <c>!</c> (a rejected ref), <c>fatal:</c>
    /// or <c>error:</c>, else the first line. A rejected push prints <c>To &lt;remote&gt;</c> first, so
    /// "the first line" told the user where the push went and never why it failed (kit#165).
    /// Same rule as <c>git-store.js</c>'s <c>reasonLine</c>.
    /// </summary>
    public static string ReasonLine(string stderr)
    {
        var lines = stderr.Trim().Split('\n').Select(l => l.Trim()).ToList();
        return lines.FirstOrDefault(l => l.StartsWith('!') || l.StartsWith("fatal:", StringComparison.Ordinal) || l.StartsWith("error:", StringComparison.Ordinal))
            ?? lines[0];
    }

    /// <summary>The top of the work tree containing <paramref name="file"/>, or null — every local run is not in one, and that is not an error.</summary>
    public static string? WorkTreeFor(string file)
    {
        var r = Git(["rev-parse", "--show-toplevel"], Path.GetDirectoryName(Path.GetFullPath(file))!);
        var top = r.Ok ? r.Stdout.Trim() : string.Empty;
        return top.Length > 0 ? top : null;
    }

    /// <summary>The branch checked out, or null in detached HEAD.</summary>
    public static string? CurrentBranch(string cwd)
    {
        var r = Git(["rev-parse", "--abbrev-ref", "HEAD"], cwd);
        var name = r.Ok ? r.Stdout.Trim() : string.Empty;
        return name.Length > 0 && name != "HEAD" ? name : null;
    }

    /// <inheritdoc />
    public IGitWriteBack WriteBack(string file, string summary, string? app)
    {
        if (!enabled)
        {
            return new GitWriteBack { Reason = "git write-back is off; the edit is in the working tree only" };
        }

        // Switched on and pointed at something that is not a repo: loud, or a deployed Kit drops every edit on restart.
        var cwd = WorkTreeFor(file);
        if (cwd is null)
        {
            return new GitWriteBack { Reason = $"git write-back is on but {Path.GetDirectoryName(file)} is not inside a git work tree" };
        }

        var target = branch ?? CurrentBranch(cwd);
        if (target is null)
        {
            return new GitWriteBack { Reason = "HEAD is detached, so there is no branch to push to; pass a branch explicitly" };
        }

        if (target == baseBranch)
        {
            return new GitWriteBack { Branch = target, Reason = $"{target} is the branch Commit proposes into, so an edit is never committed onto it; set KIT_GIT_BRANCH to an edits branch" };
        }

        var rel = Path.GetRelativePath(cwd, Path.GetFullPath(file));

        var added = Git(["add", "--", rel], cwd);
        if (!added.Ok)
        {
            return new GitWriteBack { Branch = target, Reason = added.Failure };
        }

        // Nothing staged: the write changed nothing. Success with nothing to do — never an empty commit.
        var staged = Git(["diff", "--cached", "--name-only", "--", rel], cwd);
        if (!staged.Ok)
        {
            return new GitWriteBack { Branch = target, Reason = staged.Failure };
        }

        if (staged.Stdout.Trim().Length == 0)
        {
            return new GitWriteBack { Branch = target, Reason = "the file is unchanged, so there was nothing to commit" };
        }

        // The pathspec is load-bearing: a pod's tree may be dirty with things that are not this edit.
        var committed = Git(["-c", $"user.name={name ?? DefaultName}", "-c", $"user.email={email ?? DefaultEmail}", "commit", "-m", Message(summary, app), "--", rel], cwd);
        if (!committed.Ok)
        {
            return new GitWriteBack { Branch = target, Reason = committed.Failure };
        }

        var sha = Git(["rev-parse", "HEAD"], cwd);
        var commit = sha.Ok ? sha.Stdout.Trim()[..Math.Min(10, sha.Stdout.Trim().Length)] : null;

        if (!push)
        {
            return new GitWriteBack { Committed = true, Commit = commit, Branch = target, Reason = "committed; pushing is switched off" };
        }

        // 🔴 Committed and NOT pushed — usually the branch moved on. Not retried, not rebased:
        // a tool rewriting his history unattended to turn its own status line green.
        var pushed = Git(["push", Remote, $"HEAD:{target}"], cwd);
        if (!pushed.Ok)
        {
            return new GitWriteBack { Committed = true, Commit = commit, Branch = target, Reason = pushed.Failure };
        }

        return new GitWriteBack { Committed = true, Pushed = true, Commit = commit, Branch = target };
    }

    // Node's `error.code` names: the two a spawn reaches in practice, else .NET's own sentence.
    private static string Errno(Win32Exception e) => e.NativeErrorCode switch
    {
        2 => "ENOENT",
        13 => "EACCES",
        20 => "ENOTDIR",
        _ => e.Message,
    };

    /// <summary>One git call: whether it succeeded, which layer failed if not, and what it printed.</summary>
    public sealed record Call(bool Ok, string? Failure, string Stdout, string Stderr);
}
