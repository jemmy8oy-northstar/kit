using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <inheritdoc />
public sealed partial class ProjectSource : IProjectSource
{
    public required string Owner { get; init; }

    public required string Repository { get; init; }

    public required string Branch { get; init; }

    public required string Path { get; init; }

    /// <summary>
    /// <c>owner/repo@branch:path</c>, or null for anything else — a source half-read would be read
    /// from the wrong place, so there is no default for a missing part. The path may not climb
    /// (<c>..</c>) or be absolute: it names a directory inside the repository.
    /// </summary>
    public static ProjectSource? Parse(string? text)
    {
        var m = Pattern().Match((text ?? string.Empty).Trim());
        if (!m.Success)
        {
            return null;
        }

        var path = m.Groups["path"].Value.Trim('/');
        if (path.Length == 0 || path.Split('/').Any(s => s is "" or "." or ".."))
        {
            return null;
        }

        return new ProjectSource
        {
            Owner = m.Groups["owner"].Value,
            Repository = m.Groups["repo"].Value,
            Branch = m.Groups["branch"].Value,
            Path = path,
        };
    }

    public override string ToString() => $"{Owner}/{Repository}@{Branch}:{Path}";

    [GeneratedRegex(@"^(?<owner>[A-Za-z0-9-]+)/(?<repo>[A-Za-z0-9._-]+)@(?<branch>[A-Za-z0-9._/-]+):(?<path>[^\s:]+)$")]
    private static partial Regex Pattern();
}
