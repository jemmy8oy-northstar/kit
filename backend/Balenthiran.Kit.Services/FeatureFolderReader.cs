using System.Text.RegularExpressions;

using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Reads the feature folder tree: BEH-PARSE-5 (folders are features), BEH-PARSE-6 (a file in a nested
/// feature tags its parent), and the BEH-PARSE-2 refusals for folder children.
/// </summary>
/// <remarks>
/// A folder is a feature when it holds a <c>readme.md</c>, a <c>.kit</c> file, or a child that is a
/// feature. A folder with none of those is skipped. A <c>.kit</c> file is spec text, parsed by the
/// corpus parser; a <c>readme.md</c> is the feature's description and is never parsed as a spec.
///
/// A line that is <c>in</c> and one name, on its own, in any of a folder's files is a folder tag. It is
/// blanked before the spec text is parsed, so the line numbers in the parser's messages still match the
/// file. A file in a feature that is nested under another feature must carry an <c>in</c> line naming
/// that enclosing feature.
/// </remarks>
public sealed class FeatureFolderReader(ICheckFileSystem disk, ICorpusParser parser)
{
    private const string ReadmeName = "readme.md";
    private const string KitSuffix = ".kit";

    // `in`, whitespace, one name, nothing else. A readme sentence that starts with "in" and has more
    // words after it is prose, and is not a tag.
    private static readonly Regex TagLine = new($@"^in[{CorpusParser.Ws}]+([^{CorpusParser.Ws}]+)\z", RegexOptions.CultureInvariant);

    private sealed class Dir
    {
        public required string Rel { get; init; }

        public string? ReadmeAbs { get; set; }

        public List<string> KitAbs { get; } = [];

        public List<Dir> Children { get; } = [];

        public bool IsFeature { get; set; }

        public Dir? Parent { get; set; }

        public string Name => Rel[(Rel.LastIndexOf('/') + 1)..];
    }

    // One file under a feature: its tags, and its text with the tag lines blanked (same line count).
    private sealed class Scanned(string rel, bool isReadme)
    {
        public string Rel { get; } = rel;

        public bool IsReadme { get; } = isReadme;

        public List<FolderTag> Tags { get; } = [];

        public string Stripped { get; set; } = "";
    }

    /// <summary>Reads the tree under <paramref name="root"/>. A missing root is an empty result, not an error.</summary>
    /// <exception cref="CorpusParseException">
    /// A refusal the cards name (<c>BadFeatureTag</c>, <c>MissingParentTag</c>), or a <c>.kit</c> file that does not parse.
    /// </exception>
    public FeatureFolders Read(string root)
    {
        if (!disk.IsDirectory(root))
        {
            return new FeatureFolders([], []);
        }

        var top = Walk(root, "");
        var features = new List<Dir>();
        Collect(top, null, features);

        // Declarations in order: the folders, then the `feature` blocks of each .kit file.
        var declared = features.Select(f => (Id: f.Name, At: f.Rel)).ToList();
        var scans = new Dictionary<Dir, List<Scanned>>();
        var behaviours = new List<IBehaviour>();
        foreach (var f in features)
        {
            var files = new List<Scanned>();
            if (f.ReadmeAbs is not null)
            {
                files.Add(Scan(disk.ReadText(f.ReadmeAbs), Rel(f, ReadmeName), isReadme: true));
            }

            foreach (var kitAbs in f.KitAbs)
            {
                var scan = Scan(disk.ReadText(kitAbs), Rel(f, Leaf(kitAbs)), isReadme: false);
                files.Add(scan);
                var corpus = parser.ParseCorpus(scan.Stripped, scan.Rel);
                behaviours.AddRange(corpus.Behaviours);
                declared.AddRange(corpus.Features.Select(b => (Id: b.Id, At: b.At)));
            }

            scans[f] = files;
        }

        CheckDuplicates(declared);

        var known = declared.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var tag in features.SelectMany(f => scans[f]).SelectMany(s => s.Tags))
        {
            if (!known.Contains(tag.Name))
            {
                throw new CorpusParseException("BadFeatureTag", $"{tag.At}: in names {tag.Name}, which is not a feature in this project");
            }
        }

        foreach (var f in features.Where(f => f.Parent is not null))
        {
            var untagged = scans[f].FirstOrDefault(s => !s.Tags.Any(t => t.Name == f.Parent!.Name));
            if (untagged is not null)
            {
                throw new CorpusParseException("MissingParentTag",
                    $"{untagged.Rel}: a file in feature {f.Name} must tag its parent with in {f.Parent!.Name}");
            }
        }

        CheckLoops(features, scans);

        var result = features.Select(f => new FolderFeature
        {
            Id = f.Name,
            Dir = f.Rel,
            Parent = f.Parent?.Name,
            In = scans[f].SelectMany(s => s.Tags).ToList(),
            Description = Description(scans[f]),
        }).ToList();

        return new FeatureFolders(result, behaviours);
    }

    // BEH-PARSE-2: a feature id declared twice is refused, naming both places it is declared.
    private static void CheckDuplicates(List<(string Id, string At)> declared)
    {
        for (var i = 0; i < declared.Count; i++)
        {
            var earlier = declared.Take(i).FirstOrDefault(d => d.Id == declared[i].Id);
            if (earlier.Id is not null)
            {
                throw new CorpusParseException("BadFeatureTag",
                    $"{declared[i].At}: feature {declared[i].Id} is already declared at {earlier.At}");
            }
        }
    }

    // BEH-PARSE-2: a feature that is its own ancestor, through its tags and the folders that hold it.
    // The loop is named at the first tag on it, which is the line to delete.
    private static void CheckLoops(List<Dir> features, Dictionary<Dir, List<Scanned>> scans)
    {
        var byId = features.ToDictionary(f => f.Name, StringComparer.Ordinal);
        var edges = new Dictionary<string, List<(string To, string? At)>>(StringComparer.Ordinal);
        foreach (var f in features)
        {
            var outgoing = new List<(string To, string? At)>();
            foreach (var tag in scans[f].SelectMany(s => s.Tags).Where(t => byId.ContainsKey(t.Name)))
            {
                outgoing.Add((tag.Name, tag.At));
            }

            if (f.Parent is not null)
            {
                outgoing.Add((f.Parent.Name, null));
            }

            edges[f.Name] = outgoing;
        }

        var done = new HashSet<string>(StringComparer.Ordinal);
        var path = new List<(string Id, string? Via)>();
        void Visit(string id, string? via)
        {
            var i = path.FindIndex(p => p.Id == id);
            if (i >= 0)
            {
                var at = path.Skip(i).Select(p => p.Via).Append(via).First(v => v is not null)!;
                var loop = string.Join(" → ", path.Skip(i).Select(p => p.Id).Append(id));
                throw new CorpusParseException("BadFeatureTag", $"{at}: {id} is its own ancestor: {loop}");
            }

            if (done.Contains(id))
            {
                return;
            }

            path.Add((id, via));
            foreach (var (to, at) in edges[id])
            {
                Visit(to, at);
            }

            path.RemoveAt(path.Count - 1);
            done.Add(id);
        }

        foreach (var f in features)
        {
            Visit(f.Name, null);
        }
    }

    // The readme's text without its tag lines, or null when nothing is left of it.
    private static string? Description(List<Scanned> files)
    {
        var readme = files.FirstOrDefault(s => s.IsReadme);
        var text = readme?.Stripped.Trim(CorpusParser.WsChars);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static Scanned Scan(string text, string rel, bool isReadme)
    {
        var lines = text.Split('\n');
        var scan = new Scanned(rel, isReadme);
        for (var i = 0; i < lines.Length; i++)
        {
            var m = TagLine.Match(lines[i].Trim(CorpusParser.WsChars));
            if (m.Success)
            {
                scan.Tags.Add(new FolderTag(m.Groups[1].Value, $"{rel}:{i + 1}"));
                lines[i] = "";
            }
        }

        // A readme keeps its prose, a .kit file keeps its spec, and both keep their line count.
        scan.Stripped = string.Join('\n', lines);
        return scan;
    }

    // Walks the tree. Entries come back sorted, so the order is the same on every machine.
    private Dir Walk(string abs, string rel)
    {
        var dir = new Dir { Rel = rel };
        foreach (var (name, isDir) in disk.Entries(abs))
        {
            var childAbs = $"{abs}/{name}";
            if (isDir)
            {
                dir.Children.Add(Walk(childAbs, rel.Length == 0 ? name : $"{rel}/{name}"));
            }
            else if (name == ReadmeName)
            {
                dir.ReadmeAbs = childAbs;
            }
            else if (name.EndsWith(KitSuffix, StringComparison.Ordinal))
            {
                dir.KitAbs.Add(childAbs);
            }
        }

        dir.IsFeature = dir.ReadmeAbs is not null || dir.KitAbs.Count > 0 || dir.Children.Any(c => c.IsFeature);
        return dir;
    }

    // A feature is nested under the nearest feature above it. Folders that are not features are skipped.
    private static void Collect(Dir dir, Dir? enclosing, List<Dir> into)
    {
        foreach (var child in dir.Children.Where(c => c.IsFeature))
        {
            child.Parent = enclosing;
            into.Add(child);
            Collect(child, child, into);
        }
    }

    private static string Rel(Dir feature, string name) => $"{feature.Rel}/{name}";

    private static string Leaf(string path) => path[(path.LastIndexOf('/') + 1)..];
}
