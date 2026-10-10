using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// The real disk, for <c>kit check</c>. A relative path resolves against <paramref name="baseDir"/>,
/// or against the process's directory when there is none — the CLI's case. A test passes one, so it
/// never has to move the whole process's working directory under its parallel neighbours.
/// </summary>
public sealed class CheckFileSystem(string? baseDir = null) : ICheckFileSystem
{
    // No BOM handling on the way in: File.ReadAllText strips one, Node's 'utf8' keeps it,
    // and a kept BOM is what decides whether a first-line test is a declaration.
    private static readonly System.Text.UTF8Encoding Utf8 = new(false);

    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(Full(path)) || Directory.Exists(Full(path));

    /// <inheritdoc />
    public string ReadText(string path) => Utf8.GetString(File.ReadAllBytes(Full(path)));

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, bool>> Entries(string dir) =>
        new DirectoryInfo(Full(dir)).EnumerateFileSystemInfos()
            .Select(e => new KeyValuePair<string, bool>(
                e.Name,
                e is DirectoryInfo && !e.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            .OrderBy(e => e.Key, StringComparer.Ordinal)
            .ToList();

    private string Full(string path) => baseDir is null || Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);
}
