namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>The disk as the Stage-0 gate sees it: arbitrary paths, because <c>--dir</c> and <c>--repo</c> can be anywhere.</summary>
public interface ICheckFileSystem
{
    /// <summary>A file or a directory, as Node's <c>fs.existsSync</c>.</summary>
    bool Exists(string path);

    /// <summary>The text exactly as stored — a BOM included, as Node's <c>readFileSync(…, 'utf8')</c> keeps it.</summary>
    string ReadText(string path);

    /// <summary>
    /// One directory's entries, sorted by code unit as Node's <c>readdirSync</c> returns them. A
    /// symbolic link is never a directory here, as a <c>Dirent</c> from <c>withFileTypes</c> reports it.
    /// </summary>
    IReadOnlyList<KeyValuePair<string, bool>> Entries(string dir);
}
