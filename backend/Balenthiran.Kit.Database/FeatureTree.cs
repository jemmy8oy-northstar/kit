using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// claude-code-bot#151 (docs/design/feature-tree.md): a project's spec as a folder of features under
/// <c>.kit/</c>, read as ONE corpus. <see cref="Compose"/> joins the files into one text, each after a
/// <see cref="FileMarker"/> line, so the parser and everything above it see one project and every
/// location names its real file. <see cref="Changes"/> turns an edited text back into the files it
/// changed, so a write lands in the card's own file and no other.
/// </summary>
/// <remarks>
/// Layout, relative to <c>.kit/</c>: <c>project.kit</c> (project-wide directives), then
/// <c>features/&lt;feature&gt;/[&lt;child&gt;/…]</c>. A folder is a feature, <c>FEAT-</c> plus its name
/// upper-cased. Its <c>README.md</c> (any case) explains the concept in prose and is not a spec; its first
/// <c>#</c> heading is the feature's title, and a line that is only <c>in FEAT-X</c> or <c>stage x</c> is one
/// of the feature's fields (extra parents, its stage). A folder with no README is still a feature, titled by
/// its name, whose parent is its enclosing folder. README segments are derived, so they are never written back.
/// Lives in Database, not Services, because both corpus directories (disk and GitHub) load and write a tree.
/// </remarks>
public static class FeatureTree
{
    /// <summary>The line that starts a file's text in a composed tree; the parser reads it as a comment that names a file.</summary>
    public const string FileMarker = "# kit:file ";

    /// <summary>The project-wide directives file.</summary>
    public const string ProjectFile = "project.kit";

    /// <summary>The folder under <c>.kit/</c> holding the features.</summary>
    public const string FeaturesDir = "features";

    /// <summary>A spec file's suffix.</summary>
    public const string KitSuffix = ".kit";

    /// <summary>A feature folder's description, matched in any case.</summary>
    public const string Readme = "README.md";

    /// <summary>A test mapping's suffix; one sits beside each <c>.kit</c> file.</summary>
    public const string TestsSuffix = ".tests.json";

    /// <summary>A tree project's notes, beside <see cref="ProjectFile"/>.</summary>
    public const string NotesFile = "notes.md";

    /// <summary>A tree project's schedule, beside <see cref="ProjectFile"/>.</summary>
    public const string ScheduleFile = "schedule";

    /// <summary>A tree project's noun bindings, beside <see cref="ProjectFile"/>.</summary>
    public const string BindingsFile = "bindings.json";

    private const string LastImprovedBy = "lastImprovedBy";

    private static readonly Regex Field = new(@"^(in FEAT-[A-Z0-9-]+|stage [a-z]+)\z", RegexOptions.CultureInvariant);

    private static readonly Regex Header = new(@"^[ \t]*behaviour[ \t]+([A-Z][A-Z0-9-]*)[ \t]", RegexOptions.CultureInvariant);

    private static readonly Regex AnyHeader = new(@"^[ \t]*(behaviour|feature|epic|decision)[ \t]", RegexOptions.CultureInvariant);

    private static readonly Regex InLine = new(@"^in[ \t]+(FEAT-[A-Z0-9-]+)\z", RegexOptions.CultureInvariant);

    private static readonly Regex ProjectLine = new(@"^#[ \t]*kit:project[ \t]+(\S+)[ \t]*\z", RegexOptions.CultureInvariant);

    /// <summary>The feature id a folder name stands for: <c>agent-pipeline</c> is <c>FEAT-AGENT-PIPELINE</c>.</summary>
    public static string FeatureId(string folder) =>
        "FEAT-" + Regex.Replace(folder.ToUpperInvariant(), "[^A-Z0-9]+", "-").Trim('-');

    /// <summary>Whether a path's last part is a feature's README, in any case (<c>README.md</c>, <c>readme.md</c>).</summary>
    public static bool IsReadme(string path) => Leaf(path).Equals(Readme, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a list of paths (relative to <c>.kit/</c>, <c>/</c>-separated) is a feature tree: it has at least
    /// one <c>.kit</c> file under <see cref="FeaturesDir"/>.
    /// </summary>
    public static bool IsTree(IEnumerable<string> paths) =>
        paths.Any(p => p.StartsWith(FeaturesDir + "/", StringComparison.Ordinal) && p.EndsWith(KitSuffix, StringComparison.Ordinal));

    /// <summary>
    /// Whether a path (relative to <c>.kit/</c>) is one the tree reads: <see cref="ProjectFile"/>, or a <c>.kit</c>
    /// file, a README or a <c>.tests.json</c> under <see cref="FeaturesDir"/>.
    /// </summary>
    public static bool IsTreeFile(string path) =>
        path == ProjectFile
        || (path.StartsWith(FeaturesDir + "/", StringComparison.Ordinal)
            && (path.EndsWith(KitSuffix, StringComparison.Ordinal) || path.EndsWith(TestsSuffix, StringComparison.Ordinal) || IsReadme(path)));

    /// <summary>
    /// A tree project's app name (*assumption*, docs/design/feature-tree.md): the <c># kit:project</c> line in
    /// <see cref="ProjectFile"/>, else <paramref name="fallback"/> (the repository's name) lower-cased, with
    /// anything but letters and digits made a hyphen.
    /// </summary>
    public static string ProjectName(string? projectKit, string fallback)
    {
        foreach (var line in (projectKit ?? string.Empty).Split('\n'))
        {
            var m = ProjectLine.Match(line.Trim());
            if (m.Success)
            {
                return m.Groups[1].Value;
            }
        }

        var slug = Regex.Replace(fallback.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 0 ? slug : "project";
    }

    /// <summary>
    /// The tree's files as one corpus text. <paramref name="files"/> are (path relative to <c>.kit/</c>, text);
    /// any that are not <see cref="ProjectFile"/>, a <c>.kit</c> file or a README under <see cref="FeaturesDir"/>
    /// are ignored. Order: the project file (always, empty when there is none, so a project-wide directive has a
    /// home), then folders depth-first by name, each folder's feature before its own <c>.kit</c> files (by name)
    /// and before its children.
    /// </summary>
    public static string Compose(IReadOnlyList<(string Path, string Text)> files)
    {
        var byPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (p, t) in files) byPath[p] = t;

        var sb = new StringBuilder();
        Append(sb, ProjectFile, byPath.TryGetValue(ProjectFile, out var project) ? project : string.Empty);

        foreach (var folder in Folders(byPath.Keys))
        {
            var readmePath = ReadmeOf(folder, byPath.Keys);
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
    /// What a write of <paramref name="text"/> (a <see cref="Compose"/> of <paramref name="current"/>, edited)
    /// changes: (path, new text) for each file whose text differs, and nothing for a file left alone. A card
    /// that was not in <paramref name="current"/> moves to its home (design rule 5): the folder of its first
    /// <c>in</c> feature, in the <c>.kit</c> file named for its area (<c>BEH-NOTE-3</c> → <c>note.kit</c>).
    /// </summary>
    /// <exception cref="NotWritableException">
    /// An edit to a README segment, text outside any file, or a new card with no <c>in</c> line naming a
    /// feature folder (Kit will not guess where it lives).
    /// </exception>
    public static IReadOnlyList<(string Path, string Text)> Changes(IReadOnlyList<(string Path, string Text)> current, string text)
    {
        var now = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (p, t) in current) now[p] = t;

        List<(string Path, string Text)> segments;
        try
        {
            segments = Segments(text).ToList();
        }
        catch (CorpusParseException e)
        {
            throw new NotWritableException(e.Message, e);
        }

        var known = Ids(Compose(current));
        var files = new List<(string Path, List<string> Lines)>();
        var homeless = new List<string>();
        foreach (var (path, body) in segments)
        {
            if (!IsReadme(path))
            {
                files.Add((path, [.. body.Split('\n')]));
                continue;
            }

            // A README segment is derived. The one thing that may follow it is a new card that the writer
            // appended after the last segment, which is moved to its home below; anything else is refused.
            now.TryGetValue(path, out var readme);
            var block = FeatureBlock(Dir(path), readme);
            var extra = body.StartsWith(block, StringComparison.Ordinal) ? body[block.Length..] : null;
            if (extra is null || Ids(extra).Any(known.Contains))
            {
                throw new NotWritableException($"{path}: a feature is changed in its folder's README, not through Kit");
            }

            if (extra.Trim().Length > 0)
            {
                homeless.Add(extra);
            }
        }

        // New cards: cut each from wherever the writer put it, and paste it into its home file.
        var folders = Folders(now.Keys).ToDictionary(FolderId, f => f, StringComparer.Ordinal);
        var moving = new List<(string Id, string Block)>();
        foreach (var (_, lines) in files)
        {
            foreach (var id in Ids(string.Join('\n', lines)).Where(id => !known.Contains(id)))
            {
                moving.Add((id, Cut(lines, id)));
            }
        }

        foreach (var extra in homeless)
        {
            var lines = extra.Split('\n').ToList();
            foreach (var id in Ids(extra))
            {
                moving.Add((id, Cut(lines, id)));
            }

            if (string.Join('\n', lines).Trim().Length > 0)
            {
                throw new NotWritableException($"only a new card can be added to a feature tree; this edit left text outside any card: {string.Join(' ', lines).Trim()}");
            }
        }

        foreach (var (id, block) in moving)
        {
            var tag = block.Split('\n').Select(l => InLine.Match(l.Trim())).FirstOrDefault(m => m.Success)?.Groups[1].Value
                ?? throw new NotWritableException($"{id} is a new card with no `in FEAT-…` line, so Kit does not know which feature folder it belongs in; give it one");
            if (!folders.TryGetValue(tag, out var folder))
            {
                throw new NotWritableException($"{id} is in {tag}, which is not a folder under .kit/{FeaturesDir}, so Kit does not know where to put it");
            }

            var home = $"{folder}/{Area(id)}{KitSuffix}";
            var at = files.FindIndex(f => f.Path == home);
            if (at < 0)
            {
                files.Add((home, []));
                at = files.Count - 1;
            }

            var target = files[at].Lines;
            var before = string.Join('\n', target).TrimEnd();
            target.Clear();
            target.AddRange((before.Length == 0 ? block + "\n" : before + "\n\n" + block + "\n").Split('\n'));
        }

        var changes = new List<(string Path, string Text)>();
        foreach (var (path, lines) in files)
        {
            var after = string.Join('\n', lines);
            now.TryGetValue(path, out var stored);

            // Compose drops a BOM and gives a file with no final newline one, and a missing project file an
            // empty segment; a card appended after a file and moved to its home takes that file's trailing
            // whitespace with it. None of those is a change, and a BOM the file had is kept when it does change.
            var bom = stored is not null && stored.StartsWith('\uFEFF');
            var was = bom ? stored![1..] : stored ?? string.Empty;
            if (after.TrimEnd() == was.TrimEnd())
            {
                continue;
            }

            changes.Add((path, bom ? "\uFEFF" + after : after));
        }

        return changes;
    }

    /// <summary>
    /// Every <c>.tests.json</c> under <see cref="FeaturesDir"/>, merged into one map in path order: a behaviour's
    /// tests from every file, the first of each <c>_</c> comment key, and the most recent <c>lastImprovedBy</c>.
    /// Null when there is none. A file that is not a JSON object is answered as itself, so the caller's own
    /// "not a JSON object" refusal names the problem rather than a merge hiding it.
    /// </summary>
    public static string? MergeTests(IEnumerable<(string Path, string Text)> files)
    {
        var maps = files
            .Where(f => f.Path.StartsWith(FeaturesDir + "/", StringComparison.Ordinal) && f.Path.EndsWith(TestsSuffix, StringComparison.Ordinal))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ToList();
        if (maps.Count == 0)
        {
            return null;
        }

        var merged = new JsonObject();
        foreach (var (_, text) in maps)
        {
            JsonObject map;
            try
            {
                if (JsonNode.Parse(text.TrimStart('\uFEFF')) is not JsonObject o)
                {
                    return text;
                }

                map = o;
            }
            catch (JsonException)
            {
                return text;
            }

            foreach (var (key, value) in map.ToList())
            {
                map.Remove(key);
                if (!merged.TryGetPropertyValue(key, out var held))
                {
                    merged[key] = value;
                }
                else if (held is JsonArray into && value is JsonArray more)
                {
                    foreach (var item in more.ToList())
                    {
                        more.Remove(item);
                        into.Add(item);
                    }
                }
                else if (key == LastImprovedBy && string.CompareOrdinal(On(value), On(held)) > 0)
                {
                    merged[key] = value;
                }
            }
        }

        return merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// The health check: every nested README tags its parent feature, and every behaviour in a <c>.kit</c> file
    /// under a feature folder is tagged into that folder's feature. Each problem names its file.
    /// </summary>
    public static IReadOnlyList<string> Problems(IReadOnlyList<(string Path, string Text)> files, IParsedCorpus corpus)
    {
        var problems = new List<string>();
        foreach (var (path, text) in files.Where(f => f.Path.StartsWith(FeaturesDir + "/", StringComparison.Ordinal) && IsReadme(f.Path)).OrderBy(f => f.Path, StringComparer.Ordinal))
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

    /// <summary>
    /// The tree parsed as one project, and gated: the parser's own refusals (an <c>in</c> naming no feature, a
    /// feature that is its own ancestor, an id declared twice: BEH-PARSE-2), then <see cref="Problems"/> as
    /// <c>MissingParentTag</c> (BEH-PARSE-6). What <c>kit check</c> reads.
    /// </summary>
    /// <exception cref="CorpusParseException">The first refusal, naming its file.</exception>
    public static IParsedCorpus Parse(IReadOnlyList<(string Path, string Text)> files, ICorpusParser parser)
    {
        var corpus = parser.ParseCorpus(Compose(files), ProjectFile);
        var problems = Problems(files, corpus);
        return problems.Count == 0 ? corpus : throw new CorpusParseException("MissingParentTag", string.Join('\n', problems));
    }

    /// <summary>
    /// The tree's files under a directory on disk (the project's <c>.kit/</c>), as (path relative to it, text):
    /// what <see cref="Compose"/>, <see cref="MergeTests"/> and <see cref="Problems"/> read. Sorted by path.
    /// </summary>
    /// <param name="disk">The disk; text is read as stored, a BOM included.</param>
    /// <param name="root">The <c>.kit/</c> directory.</param>
    public static IReadOnlyList<(string Path, string Text)> ReadFiles(ICheckFileSystem disk, string root)
    {
        var output = new List<(string Path, string Text)>();
        var project = $"{root}/{ProjectFile}";
        if (disk.Exists(project) && !disk.IsDirectory(project))
        {
            output.Add((ProjectFile, disk.ReadText(project)));
        }

        void Walk(string rel)
        {
            foreach (var (name, isDir) in disk.Entries($"{root}/{rel}"))
            {
                var path = $"{rel}/{name}";
                if (isDir)
                {
                    Walk(path);
                }
                else if (IsTreeFile(path))
                {
                    output.Add((path, disk.ReadText($"{root}/{path}")));
                }
            }
        }

        if (disk.IsDirectory($"{root}/{FeaturesDir}"))
        {
            Walk(FeaturesDir);
        }

        return output.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
    }

    // Every segment of a composed text, README ones included, in order.
    private static IEnumerable<(string Path, string Text)> Segments(string text)
    {
        var parts = new List<(string Path, StringBuilder Text)>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var raw = line.TrimEnd('\r');
            if (raw.StartsWith(FileMarker, StringComparison.Ordinal) && raw.Length > FileMarker.Length)
            {
                parts.Add((raw[FileMarker.Length..], new StringBuilder()));
                continue;
            }

            var last = i == lines.Length - 1;
            if (parts.Count == 0)
            {
                if (line.Trim().Length > 0) throw new CorpusParseException($"line {i + 1}: text before the first `{FileMarker.Trim()}` line belongs to no file");
                continue;
            }

            parts[^1].Text.Append(line);
            if (!last) parts[^1].Text.Append('\n');
        }

        return parts.Select(p => (p.Path, p.Text.ToString()));
    }

    // The behaviour ids a text declares, in order.
    private static List<string> Ids(string text) =>
        text.Split('\n').Select(l => Header.Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();

    // Removes a behaviour's block (header to its last content line) and one separator blank, as
    // CorpusWriter.RemoveBehaviour does, and answers the block's text.
    private static string Cut(List<string> lines, string id)
    {
        var start = lines.FindIndex(l => Header.Match(l) is { Success: true } m && m.Groups[1].Value == id);
        var end = start;
        for (var i = start + 1; i < lines.Count && !AnyHeader.IsMatch(lines[i]); i++)
        {
            var t = lines[i].Trim();
            if (t.Length > 0 && !t.StartsWith('#')) end = i;
        }

        var block = string.Join('\n', lines.Skip(start).Take(end - start + 1).Select(l => l.TrimEnd('\r')));
        var from = start;
        var count = end - start + 1;
        if (end + 1 < lines.Count - 1 && lines[end + 1].Trim().Length == 0)
        {
            count++;
        }
        else if (start > 0 && lines[start - 1].Trim().Length == 0)
        {
            from--;
            count++;
        }

        lines.RemoveRange(from, count);
        return block;
    }

    // `BEH-NOTE-3` → `note`; `BEH-AGENT-RUN-2` → `agent-run`; an id with no area is its own.
    private static string Area(string id)
    {
        var parts = id.Split('-');
        var middle = parts.Length >= 3 ? parts[1..^1] : parts.Length == 2 ? parts[..1] : parts;
        return string.Join('-', middle).ToLowerInvariant();
    }

    private static string? On(JsonNode? node) => node is JsonObject o && o["on"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    // Every feature folder the paths imply, outermost first, depth-first by name.
    private static IEnumerable<string> Folders(IEnumerable<string> paths) =>
        paths
            .Where(p => p.StartsWith(FeaturesDir + "/", StringComparison.Ordinal))
            .Where(p => p.EndsWith(KitSuffix, StringComparison.Ordinal) || IsReadme(p))
            .SelectMany(p => Ancestors(Dir(p)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static string FolderId(string folder) => FeatureId(Leaf(folder));

    // A folder's README path as stored, in whatever case it was written; README.md when it has none.
    private static string ReadmeOf(string folder, IEnumerable<string> paths) =>
        paths.Where(p => Dir(p) == folder && IsReadme(p)).Order(StringComparer.Ordinal).FirstOrDefault() ?? folder + "/" + Readme;

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
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
        sb.Append(FileMarker).Append(path).Append('\n').Append(text);
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
