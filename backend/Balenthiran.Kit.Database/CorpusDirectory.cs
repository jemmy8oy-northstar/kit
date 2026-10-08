using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// Kit's storage is files in git (kit#117), so this is its read store: one
/// directory of corpora, and the repository root the view's paths are relative to.
/// </summary>
public sealed class CorpusDirectory(string dir, string repoRoot) : ICorpusDirectory
{
    private const string Suffix = ".beh";

    /// <inheritdoc />
    public IReadOnlyList<string> Corpora() =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*" + Suffix)
                .Select(Path.GetFileName)
                .Where(f => f!.EndsWith(Suffix, StringComparison.Ordinal))
                .Select(f => f![..^Suffix.Length])
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    /// <inheritdoc />
    public string Read(string app) => File.ReadAllText(Path.Combine(dir, app + Suffix));

    /// <inheritdoc />
    public JsonObject Bindings(string app)
    {
        var file = Path.Combine(dir, app + ".bindings.json");

        // A file that exists and will not parse still throws: that is could-not-look.
        return File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file))!.AsObject() : [];
    }

    /// <inheritdoc />
    public string RelativePath(string app) => Relative(Path.Combine(dir, app + Suffix));

    /// <inheritdoc />
    public string RelativeBindingsPath(string app) => Relative(BindingsFile(app));

    /// <inheritdoc />
    public string FullPath(string app) => Path.GetFullPath(Path.Combine(dir, app + Suffix));

    /// <inheritdoc />
    public string FullBindingsPath(string app) => Path.GetFullPath(BindingsFile(app));

    /// <inheritdoc />
    public string ReadText(string app) => Utf8.GetString(File.ReadAllBytes(Path.Combine(dir, app + Suffix)));

    /// <inheritdoc />
    public string? ReadBindingsText(string app) => File.Exists(BindingsFile(app)) ? Utf8.GetString(File.ReadAllBytes(BindingsFile(app))) : null;

    /// <inheritdoc />
    public void WriteText(string app, string text) => File.WriteAllBytes(Path.Combine(dir, app + Suffix), Utf8.GetBytes(text));

    /// <inheritdoc />
    public void WriteBindingsText(string app, string text) => File.WriteAllBytes(BindingsFile(app), Utf8.GetBytes(text));

    // `readFileSync(f, 'utf8')` / `writeFileSync(f, s)`: a BOM is a character like any
    // other, kept on the way in and written back on the way out. File.ReadAllText would
    // strip it, so a write would silently change a line no one asked it to.
    private static readonly System.Text.UTF8Encoding Utf8 = new(false);

    private string BindingsFile(string app) => Path.Combine(dir, app + ".bindings.json");

    private string Relative(string file) => Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
}
