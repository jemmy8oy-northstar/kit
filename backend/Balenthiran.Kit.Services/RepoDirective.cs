using System.Text.RegularExpressions;

namespace Balenthiran.Kit.Services;

/// <summary>
/// <c># kit:repo owner/repo@branch</c> (BEH-HEALTH-1): where an app's tests live. Read from the
/// corpus text like <c>kit:idea</c> and <c>kit:principle</c>, so the parser is untouched.
/// </summary>
public static class RepoDirective
{
    private static readonly Regex Line = new(
        $@"^#[{CorpusParser.Ws}]*kit:repo(?![A-Za-z0-9_-])(.*)\z", RegexOptions.Compiled);

    private static readonly Regex Shape = new(
        $@"^[{CorpusParser.Ws}]+([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[^\s:]+)[{CorpusParser.Ws}]*\z", RegexOptions.Compiled);

    /// <summary>
    /// The repo the file names, <c>null</c> when it names none, or a <c>Problem</c> when a
    /// <c>kit:repo</c> line is there but is not <c>owner/repo@branch</c> — a typo must not read as "no line".
    /// </summary>
    public static (string? Repo, string? Problem) Read(string src)
    {
        foreach (var line in src.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var m = Line.Match(line);
            if (!m.Success)
            {
                continue;
            }

            var shape = Shape.Match(m.Groups[1].Value);
            return shape.Success ? (shape.Groups[1].Value, null) : (null, $"`# kit:repo` wants owner/repo@branch, got: {m.Groups[1].Value.Trim()}");
        }

        return (null, null);
    }
}
