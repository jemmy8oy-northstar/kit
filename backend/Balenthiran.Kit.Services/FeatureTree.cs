using System.Text;
using System.Text.RegularExpressions;

using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;

namespace Balenthiran.Kit.Services;

/// <summary>
/// claude-code-bot#151 (docs/design/feature-tree.md): a project's spec as a folder of features under
/// <c>.kit/</c>, read as ONE corpus. <see cref="Compose"/> joins the files into one text, each after a
/// <see cref="CorpusParser.FileMarker"/> line, so the parser and everything above it see one project and
/// every location names its real file. <see cref="Split"/> turns an edited text back into the files it came
/// from, so a write lands in the card's own file and no other.
/// </summary>
/// <remarks>
/// Layout, relative to <c>.kit/</c>: <c>project.kit</c> (project-wide directives), then
/// <c>features/&lt;feature&gt;/[&lt;child&gt;/…]</c>. A folder is a feature, <c>FEAT-</c> plus its name
/// upper-cased. Its <c>README.md</c> explains the concept in prose and is not a spec; its first <c>#</c>
/// heading is the feature's title, and a line that is only <c>in FEAT-X</c> or <c>stage x</c> is one of the
/// feature's fields (extra parents, its stage). A folder with no README is still a feature, titled by its
/// name, whose parent is its enclosing folder. README segments are derived, so they are never written back.
/// </remarks>
public static class FeatureTree
{
    /// <summary>The project-wide directives file.</summary>
    public const string ProjectFile = "project.kit";

    /// <summary>The folder under <c>.kit/</c> holding the features.</summary>
    public const string FeaturesDir = "features";

    /// <summary>A spec file's suffix.</summary>
    public const string KitSuffix = ".kit";

    /// <summary>A feature folder's description.</summary>
    public const string Readme = "README.md";

    private static readonly Regex Field = new(@"^(in FEAT-[A-Z0-9-]+|stage [a-z]+)\z", RegexOptions.CultureInvariant);

    /// <summary>The feature id a folder name stands for: <c>agent-pipeline</c> is <c>FEAT-AGENT-PIPELINE</c>.</summary>
    public static string FeatureId(string folder) =>
        "FEAT-" + Regex.Replace(folder.ToUpperInvariant(), "[^A-Z0-9]+", "-").Trim('-');

    /// <summary>
    /// Whether a list of paths (relative to <c>.kit/</c>, <c>/</c>-separated) is a feature tree: it has at least
    /// one <c>.kit</c> file under <see cref="FeaturesDir"/>.
    /// </summary>
    public static bool IsTree(IEnumerable<string> paths) =>
        paths.Any(p => p.StartsWith(FeaturesDir + "/", StringComparison.Ordinal) && p.EndsWith(KitSuffix, StringComparison.Ordinal));

    /// <summary>
    /// The tree's files as one corpus text. <paramref name="files"/> are (path relative to <c>.kit/</c>, text);
    /// any that are not <see cref="ProjectFile"/>, a <c>.kit</c> file or a README under <see cref="FeaturesDir"/>
    /// are ignored. Order: the project file, then folders depth-first by name, each folder's feature before its
    /// own <c>.kit</c> files (by name) and before its children.
    /// </summary>
    public static string Compose(IReadOnlyList<(string Path, string Text)> files)
    {
        var byPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (p, t) in files) byPath[p] = t;

        var sb = new StringBuilder();
        if (byPath.TryGetValue(ProjectFile, out var project)) Append(sb, ProjectFile, project);

        var folders = byPath.Keys
            .Where(p => p.StartsWith(FeaturesDir + "/", StringComparison.Ordinal))
            .Where(p => p.EndsWith(KitSuffix, StringComparison.Ordinal) || p.EndsWith("/" + Readme, StringComparison.Ordinal))
            .SelectMany(p => Ancestors(Dir(p)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        foreach (var folder in folders)
        {
            var readmePath = folder + "/" + Readme;
            byPath.TryGetValue(readmePath, out var readme);
            Append(sb, readmePath, FeatureBlock(folder, readme));
            var kits = byPath.Keys
                .Where(p => Dir(p) == folder && p.EndsWith(KitSuffix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal);
            foreach (var k in kits) Append(sb, k, byPath[k]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// The files an edited composed text stands for: (path, text) for <see cref="ProjectFile"/> and each
    /// <c>.kit</c> file. README segments are derived from the README, so they are checked, not returned: an
    /// edit to one is refused, because a feature is changed in its folder, not through Kit.
    /// </summary>
    /// <exception cref="CorpusParseException">Text before the first file line, or an edited README segment.</exception>
    public static IReadOnlyList<(string Path, string Text)> Split(string text, IReadOnlyDictionary<string, string?>? readmes = null)
    {
        var parts = new List<(string Path, StringBuilder Text)>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var raw = line.TrimEnd('\r');
            if (raw.StartsWith(CorpusParser.FileMarker, StringComparison.Ordinal) && raw.Length > CorpusParser.FileMarker.Length)
            {
                parts.Add((raw[CorpusParser.FileMarker.Length..], new StringBuilder()));
                continue;
            }

            var last = i == lines.Length - 1;
            if (parts.Count == 0)
            {
                if (line.Trim().Length > 0) throw new CorpusParseException($"line {i + 1}: text before the first `{CorpusParser.FileMarker.Trim()}` line belongs to no file");
                continue;
            }

            parts[^1].Text.Append(line);
            if (!last) parts[^1].Text.Append('\n');
        }

        var output = new List<(string Path, string Text)>();
        foreach (var (path, sb) in parts)
        {
            if (path.EndsWith("/" + Readme, StringComparison.Ordinal))
            {
                if (readmes is not null)
                {
                    readmes.TryGetValue(path, out var readme);
                    if (sb.ToString() != FeatureBlock(Dir(path), readme))
                    {
                        throw new CorpusParseException($"{path}: a feature is changed in its folder's README, not through Kit");
                    }
                }

                continue;
            }

            output.Add((path, sb.ToString()));
        }

        return output;
    }

    /// <summary>
    /// The health check: every nested README tags its parent feature, and every behaviour in a <c>.kit</c> file
    /// under a feature folder is tagged into that folder's feature. Each problem names its file.
    /// </summary>
    public static IReadOnlyList<string> Problems(IReadOnlyList<(string Path, string Text)> files, IParsedCorpus corpus)
    {
        var problems = new List<string>();
        foreach (var (path, text) in files.Where(f => f.Path.EndsWith("/" + Readme, StringComparison.Ordinal)).OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            var parent = ParentFolder(Dir(path));
            if (parent is null) continue;
            var want = "in " + FeatureId(Leaf(parent));
            if (!Fields(text).Contains(want)) problems.Add($"{path}: a nested feature's README must tag its parent with `{want}`");
        }

        foreach (var b in corpus.Behaviours)
        {
            var file = b.At[..b.At.LastIndexOf(':')];
            if (!file.StartsWith(FeaturesDir + "/", StringComparison.Ordinal)) continue;
            var want = FeatureId(Leaf(Dir(file)));
            if (!b.In.Any(t => t.Id == want)) problems.Add($"{b.At}: {b.Id} is in {Dir(file)}/, so it must be tagged `in {want}`");
        }

        return problems;
    }

    // The derived text a folder's feature contributes: its declaration, the implicit parent tag when the
    // README does not give one, and the README's own field lines.
    private static string FeatureBlock(string folder, string? readme)
    {
        var title = readme is null ? null : Title(readme);
        var fields = readme is null ? [] : Fields(readme);
        var sb = new StringBuilder();
        sb.Append("feature ").Append(FeatureId(Leaf(folder))).Append(" \"").Append((title ?? Leaf(folder)).Replace("\"", "'")).Append("\"\n");
        var parent = ParentFolder(folder);
        var parentTag = parent is null ? null : "in " + FeatureId(Leaf(parent));
        if (parentTag is not null && !fields.Contains(parentTag)) sb.Append("  ").Append(parentTag).Append('\n');
        foreach (var f in fields) sb.Append("  ").Append(f).Append('\n');
        return sb.ToString();
    }

    private static string? Title(string readme) =>
        readme.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim();

    private static List<string> Fields(string readme) =>
        readme.Split('\n').Select(l => l.Trim()).Where(l => Field.IsMatch(l)).Distinct(StringComparer.Ordinal).ToList();

    private static void Append(StringBuilder sb, string path, string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        sb.Append(CorpusParser.FileMarker).Append(path).Append('\n').Append(text);
        if (text.Length > 0 && !text.EndsWith('\n')) sb.Append('\n');
    }

    private static string Dir(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";

    private static string Leaf(string path) => path[(path.LastIndexOf('/') + 1)..];

    // A feature folder's enclosing feature folder, or null for a top-level feature.
    private static string? ParentFolder(string folder)
    {
        var up = Dir(folder);
        return up == FeaturesDir ? null : up;
    }

    // A folder and each enclosing feature folder, outermost first.
    private static IEnumerable<string> Ancestors(string folder)
    {
        var chain = new List<string>();
        for (var f = folder; f != FeaturesDir && f.Contains('/'); f = Dir(f)) chain.Add(f);
        chain.Reverse();
        return chain;
    }
}
