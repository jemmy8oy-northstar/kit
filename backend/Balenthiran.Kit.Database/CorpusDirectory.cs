using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// Kit's storage is files in git (kit#117), so this is its read store: one
/// directory of corpora, and the repository root the view's paths are relative to.
/// </summary>
/// <remarks>
/// claude-code-bot#151: when the directory holds a feature tree (<c>features/**/*.kit</c>, see
/// <see cref="FeatureTree"/>), the tree is ONE more app, named by <c>project.kit</c>'s <c># kit:project</c>
/// line or else the repository's folder. Its text is the tree composed; a write goes back to each file it
/// changed; its tests are every <c>.tests.json</c> in the tree merged; its notes, schedule and bindings are
/// <c>notes.md</c>, <c>schedule</c> and <c>bindings.json</c> beside <c>project.kit</c>. With no tree, every
/// app is a flat <c>&lt;app&gt;.beh</c> exactly as before, and a flat file with the tree's name is hidden.
/// </remarks>
public sealed class CorpusDirectory(string dir, string repoRoot) : ICorpusDirectory
{
    private const string Suffix = ".beh";

    private static readonly CheckFileSystem Disk = new();

    /// <inheritdoc />
    public IReadOnlyList<string> Corpora()
    {
        var flat = Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*" + Suffix)
                .Select(Path.GetFileName)
                .Where(f => f!.EndsWith(Suffix, StringComparison.Ordinal))
                .Select(f => f![..^Suffix.Length])
            : [];
        var tree = TreeApp();
        return (tree is null ? flat : flat.Where(a => a != tree).Append(tree))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public string Read(string app) => IsTree(app) ? Compose() : File.ReadAllText(Path.Combine(dir, app + Suffix));

    /// <inheritdoc />
    public JsonObject Bindings(string app)
    {
        var file = BindingsFile(app);

        // A file that exists and will not parse still throws: that is could-not-look.
        return !File.Exists(file) ? []
            : JsonNode.Parse(File.ReadAllText(file)) as JsonObject
                ?? throw new InvalidOperationException("bindings.json is not a JSON object");
    }

    /// <inheritdoc />
    public string RelativePath(string app) => Relative(CorpusFile(app));

    /// <inheritdoc />
    public string RelativeBindingsPath(string app) => Relative(BindingsFile(app));

    /// <inheritdoc />
    public string FullPath(string app) => Path.GetFullPath(CorpusFile(app));

    /// <inheritdoc />
    public string FullBindingsPath(string app) => Path.GetFullPath(BindingsFile(app));

    /// <inheritdoc />
    public string ReadText(string app) => IsTree(app) ? Compose() : Utf8.GetString(File.ReadAllBytes(Path.Combine(dir, app + Suffix)));

    /// <inheritdoc />
    public string? ReadBindingsText(string app) => File.Exists(BindingsFile(app)) ? Utf8.GetString(File.ReadAllBytes(BindingsFile(app))) : null;

    /// <inheritdoc />
    /// <remarks>A tree app writes each file the edit changed, and no other (claude-code-bot#151).</remarks>
    public void WriteText(string app, string text)
    {
        if (!IsTree(app))
        {
            Replace(Path.Combine(dir, app + Suffix), text);
            return;
        }

        foreach (var (path, changed) in FeatureTree.Changes(FeatureTree.ReadFiles(Disk, dir), text))
        {
            var file = Path.Combine(dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            Replace(file, changed);
        }
    }

    /// <inheritdoc />
    public void WriteBindingsText(string app, string text) => Replace(BindingsFile(app), text);

    /// <inheritdoc />
    public string? ReadNotesText(string app) => File.Exists(NotesFile(app)) ? Utf8.GetString(File.ReadAllBytes(NotesFile(app))) : null;

    /// <inheritdoc />
    public void WriteNotesText(string app, string text) => Replace(NotesFile(app), text);

    /// <inheritdoc />
    public string RelativeNotesPath(string app) => Relative(NotesFile(app));

    /// <inheritdoc />
    public string FullNotesPath(string app) => Path.GetFullPath(NotesFile(app));

    /// <inheritdoc />
    public string? ReadScheduleText(string app) => File.Exists(ScheduleFile(app)) ? Utf8.GetString(File.ReadAllBytes(ScheduleFile(app))) : null;

    /// <inheritdoc />
    public string? ReadTestsText(string app)
    {
        if (IsTree(app))
        {
            return FeatureTree.MergeTests(FeatureTree.ReadFiles(Disk, dir));
        }

        var file = Path.Combine(dir, app + ".tests.json");
        return File.Exists(file) ? Utf8.GetString(File.ReadAllBytes(file)) : null;
    }

    /// <inheritdoc />
    public void WriteScheduleText(string app, string text) => Replace(ScheduleFile(app), text);

    /// <inheritdoc />
    public string RelativeSchedulePath(string app) => Relative(ScheduleFile(app));

    /// <inheritdoc />
    public string FullSchedulePath(string app) => Path.GetFullPath(ScheduleFile(app));

    // Write beside the file, then rename over it. File.WriteAllBytes truncates first, and
    // Kestrel serves reads while a write is in flight (no GET takes the edit lock), so a page
    // could read an empty or half-written corpus — or throw, when the file shrank mid-read.
    // A rename within one directory is atomic, so a reader sees the old file or the new one.
    private static void Replace(string file, string text)
    {
        var temp = Path.Combine(Path.GetDirectoryName(file)!, "." + Path.GetFileName(file) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllBytes(temp, Utf8.GetBytes(text));
            File.Move(temp, file, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    // `readFileSync(f, 'utf8')` / `writeFileSync(f, s)`: a BOM is a character like any
    // other, kept on the way in and written back on the way out. File.ReadAllText would
    // strip it, so a write would silently change a line no one asked it to.
    private static readonly System.Text.UTF8Encoding Utf8 = new(false);

    // The tree's app name, or null when the directory holds no tree.
    private string? TreeApp()
    {
        var features = Path.Combine(dir, FeatureTree.FeaturesDir);
        if (!Directory.Exists(features) || !Directory.EnumerateFiles(features, "*" + FeatureTree.KitSuffix, SearchOption.AllDirectories).Any())
        {
            return null;
        }

        var project = Path.Combine(dir, FeatureTree.ProjectFile);
        return FeatureTree.ProjectName(File.Exists(project) ? File.ReadAllText(project) : null, RepoName());
    }

    // The folder the tree's .kit/ sits in: `.kit` itself names nothing.
    private string RepoName()
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir));
        var name = Path.GetFileName(full);
        return name.StartsWith('.') ? Path.GetFileName(Path.GetDirectoryName(full) ?? name) : name;
    }

    private bool IsTree(string app) => TreeApp() == app;

    private string Compose() => FeatureTree.Compose(FeatureTree.ReadFiles(Disk, dir));

    // A tree's "file" is its directory: a git write-back commits every file under it that changed.
    private string CorpusFile(string app) => IsTree(app) ? dir : Path.Combine(dir, app + Suffix);

    private string BindingsFile(string app) => Path.Combine(dir, IsTree(app) ? FeatureTree.BindingsFile : app + ".bindings.json");

    private string NotesFile(string app) => Path.Combine(dir, IsTree(app) ? FeatureTree.NotesFile : app + ".notes.md");

    private string ScheduleFile(string app) => Path.Combine(dir, IsTree(app) ? FeatureTree.ScheduleFile : app + ".schedule");

    private string Relative(string file) => Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
}
