using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-HEALTH-4's two pure halves: which source files a coverage run touched, and the one hash
/// over them. Neither starts a process or reads the clock, so the same files give the same
/// hash on any machine — the property the whole feature stands on.
/// </summary>
public static class LinkHasher
{
    /// <summary>
    /// SHA-256 (lower hex) over <c>path\ncontent\n</c> for each path in ordinal-sorted order, with
    /// <c>\r\n</c> in the content read as <c>\n</c> so a Windows checkout hashes as a Linux one.
    /// Paths are repo-relative with <c>/</c>; the order the caller lists them in is irrelevant.
    /// Throws <see cref="FileNotFoundException"/> if one is gone — a missing file is a change.
    /// </summary>
    public static string Hash(string repoRoot, IEnumerable<string> paths)
    {
        using var sha = SHA256.Create();
        foreach (var path in paths.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
        {
            var content = File.ReadAllText(Path.Combine(repoRoot, path)).Replace("\r\n", "\n");
            var bytes = Encoding.UTF8.GetBytes(path + "\n" + content + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexStringLower(sha.Hash!);
    }

    /// <summary>
    /// The repo-relative files a Cobertura report shows were run: any class line with
    /// <c>hits &gt; 0</c> marks its file. A file whose lines are all zero was loaded, not exercised.
    /// Filenames are resolved through the report's <c>&lt;sources&gt;</c> (coverlet writes them
    /// relative to one), then kept only if they exist under the repo and are not build output —
    /// <c>obj/</c> carries generated files that differ per machine.
    /// </summary>
    public static IReadOnlyList<string> ExercisedFiles(string coberturaXml, string repoRoot)
    {
        var doc = XDocument.Parse(coberturaXml);
        var sources = doc.Descendants("source").Select(s => s.Value.Trim()).Where(s => s.Length > 0).ToList();
        var root = Path.GetFullPath(repoRoot).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var cls in doc.Descendants("class"))
        {
            var name = (string?)cls.Attribute("filename");
            var hit = cls.Descendants("line").Any(l => int.TryParse((string?)l.Attribute("hits"), out var n) && n > 0);
            if (name is null || !hit)
            {
                continue;
            }

            foreach (var source in sources.DefaultIfEmpty(root))
            {
                var full = Path.GetFullPath(Path.Combine(source, name));
                if (!full.StartsWith(root, StringComparison.Ordinal) || !File.Exists(full))
                {
                    continue;
                }

                var rel = full[root.Length..].Replace('\\', '/');
                if (!rel.Split('/').Any(part => part is "obj" or "bin"))
                {
                    found.Add(rel);
                }

                break;
            }
        }

        return [.. found];
    }
}
