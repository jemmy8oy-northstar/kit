namespace Balenthiran.Kit.Services;

/// <summary>
/// A feature read from a folder under <c>.kit/features</c> (BEH-PARSE-5). Its id is the folder name;
/// <see cref="Parent"/> is the nearest feature folder around it; <see cref="In"/> is every tag its
/// files carry, kept as display tags and never as a dependency.
/// </summary>
public sealed class FolderFeature
{
    public required string Id { get; init; }

    /// <summary>The folder, relative to <c>.kit/features</c>, with <c>/</c> between segments.</summary>
    public required string Dir { get; init; }

    public string? Parent { get; init; }

    public required IReadOnlyList<FolderTag> In { get; init; }

    /// <summary>The <c>readme.md</c> text without its <c>in</c> lines, or null when there is none.</summary>
    public string? Description { get; init; }
}

