namespace Balenthiran.Kit.Services;

/// <summary>An <c>in</c> line in a folder's file: the feature it names, and where the line is (<c>file:line</c>).</summary>
public sealed record FolderTag(string Name, string At);
