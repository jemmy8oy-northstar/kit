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
    public string RelativePath(string app) =>
        Path.GetRelativePath(repoRoot, Path.Combine(dir, app + Suffix)).Replace('\\', '/');
}
