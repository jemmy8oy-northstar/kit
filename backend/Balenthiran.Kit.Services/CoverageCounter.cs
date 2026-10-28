using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Counting moved out of <see cref="KitCheck"/> so hosted Kit (BEH-HEALTH-1) and the CLI gate
/// cannot disagree about what "a test names it" means. Pure: it reads no disk and no network.
/// </summary>
public sealed class CoverageCounter(ITestTitleReader reader) : ICoverageCounter
{
    private static readonly HashSet<string> SkipDir = new(StringComparer.Ordinal)
    {
        "node_modules", ".git", "bin", "obj", "dist", "build", ".next", "coverage", "playwright-report", "test-results",
        ".build", "DerivedData",
    };

    /// <inheritdoc />
    public bool SkipsDirectory(string name) => SkipDir.Contains(name);

    /// <inheritdoc />
    public bool IsTestPath(string path)
    {
        var parts = path.Split('/');
        return reader.IsTestFile(parts[^1]) && !parts[..^1].Any(SkipDir.Contains);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A reader whose two counts disagree is losing or inventing tests, so that is a refusal,
    /// never a number.
    /// </remarks>
    public ITestReading ReadTitles(IEnumerable<(string File, string Source)> files)
    {
        var titles = new List<ITestTitle>();
        foreach (var (f, src) in files)
        {
            var got = reader.Titles(f, src);
            var want = reader.ExpectedCount(f, src);
            if (got.Count != want)
            {
                var evidence = f.EndsWith(".cs", StringComparison.Ordinal)
                    ? $"{want} [Fact]/[Theory] attribute(s) exist"
                    : $"{want} test declaration(s) survive stripping strings and comments";
                return new TestReading(titles, $"{f}: read {got.Count} test(s) but {evidence} — the two counts disagree, so the reader is losing or inventing tests");
            }

            titles.AddRange(got);
        }

        return new TestReading(titles, null);
    }

    /// <inheritdoc />
    public ICoverageResult Count(IReadOnlyList<IBehaviour> behaviours, JsonObject map, IReadOnlyList<ITestTitle> titles, string mapPath)
    {
        var result = Mapping(behaviours, map, titles, mapPath);
        if (result.Refusal is null)
        {
            result.Covered = behaviours.Where(b => result.LinkedFiles.ContainsKey(b.Id)).ToList();
            var unlinked = behaviours.Where(b => !result.LinkedFiles.ContainsKey(b.Id)).ToList();

            // kit#155: a pending behaviour with no test is expected, and is not in the denominator.
            result.Uncovered = unlinked.Where(b => !b.Pending).ToList();
            result.Pending = unlinked.Where(b => b.Pending).ToList();
        }

        return result;
    }

    // Option C: the corpus carries behaviour → test, keyed on file + title (a title alone cannot
    // address a duplicate). It rots LOUDLY — every entry must name a test that exists.
    private static CoverageResult Mapping(IReadOnlyList<IBehaviour> behaviours, JsonObject map, IReadOnlyList<ITestTitle> titles, string mapPath)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var t in titles)
        {
            var k = t.File + "\0" + t.Raw;
            index[k] = index.GetValueOrDefault(k) + 1;
        }

        var files = titles.Select(t => t.File).ToHashSet(StringComparer.Ordinal);
        var ids = behaviours.Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
        var result = new CoverageResult();

        foreach (var (id, node) in JsValue.Entries(map))
        {
            if (id.StartsWith('_'))
            {
                continue;
            }

            if (!ids.Contains(id))
            {
                result.Errors.Add($"{id}: mapped to a test, but no such behaviour in the corpus");
                continue;
            }

            if (node is not JsonArray entries)
            {
                result.Refusal = $"{mapPath}: {id} must be an array of {{ file, title }}";
                return result;
            }

            foreach (var entry in entries)
            {
                var file = Field(entry, "file");
                var title = Field(entry, "title");
                if (file is null || !files.Contains(file))
                {
                    result.Errors.Add($"{id}: names {file ?? "undefined"}, which is not a test file in this app");
                    continue;
                }

                // Zero and two are different failures: a test that moved, and a key that
                // cannot address what it names.
                var n = index.GetValueOrDefault(file + "\0" + (title ?? "undefined"));
                if (n == 0)
                {
                    result.Errors.Add($"{id}: {file} has no test titled \"{title ?? "undefined"}\" — renamed or deleted?");
                    continue;
                }

                if (n > 1)
                {
                    result.Errors.Add($"{id}: \"{title}\" appears {n}× in {file} — file+title cannot address it uniquely");
                    continue;
                }

                if (!result.LinkedFiles.TryGetValue(id, out var linked))
                {
                    result.LinkedFiles[id] = linked = [];
                }

                linked.Add(file);
            }
        }

        return result;
    }

    private static string? Field(JsonNode? entry, string key) =>
        entry is JsonObject o && o.TryGetPropertyValue(key, out var v) && v is JsonValue s && s.TryGetValue<string>(out var text) ? text : null;
}
