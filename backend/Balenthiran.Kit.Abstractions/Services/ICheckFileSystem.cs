namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// The disk as the <c>kit</c> commands see it: arbitrary paths, because <c>--dir</c> and
/// <c>--repo</c> can be anywhere.
/// </summary>
public interface ICheckFileSystem
{
    /// <summary>A file or a directory, as Node's <c>fs.existsSync</c>.</summary>
    bool Exists(string path);

    /// <summary>A directory, and not a file.</summary>
    bool IsDirectory(string path);

    /// <summary>
    /// The absolute path, as Node's <c>path.resolve</c> prints it: normalised, and with no
    /// trailing separator. Relative paths resolve against the directory the commands run from.
    /// </summary>
    string Resolve(string path);

    /// <summary>The text exactly as stored — a BOM included, as Node's <c>readFileSync(…, 'utf8')</c> keeps it.</summary>
    string ReadText(string path);

    /// <summary>
    /// One directory's entries, sorted by code unit as Node's <c>readdirSync</c> returns them. A
    /// symbolic link is never a directory here, as a <c>Dirent</c> from <c>withFileTypes</c> reports it.
    /// </summary>
    IReadOnlyList<KeyValuePair<string, bool>> Entries(string dir);
}
