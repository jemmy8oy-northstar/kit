using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// The one place coverage is counted (BEH-HEALTH-1): <c>kit check</c> reads a repo from disk and
/// hosted Kit reads it from GitHub, and both must mean the same thing by "a test names it".
/// </summary>
public interface ICoverageCounter
{
    /// <summary>Whether a directory is never walked for tests: <c>node_modules</c>, <c>bin</c>, build output.</summary>
    bool SkipsDirectory(string name);

    /// <summary>Whether a repo-relative, <c>/</c>-separated path is a test file the gate reads and no skipped directory holds.</summary>
    bool IsTestPath(string path);

    /// <summary>
    /// Read each file's titles and cross-check them against the second count. Lazy: the first
    /// disagreement stops the enumeration, because it is a refusal and nothing after it matters.
    /// </summary>
    ITestReading ReadTitles(IEnumerable<(string File, string Source)> files);

    /// <summary>Decide covered, uncovered and pending from the mapping keyed on file + title.</summary>
    ICoverageResult Count(IReadOnlyList<IBehaviour> behaviours, JsonObject map, IReadOnlyList<ITestTitle> titles, string mapPath);
}
