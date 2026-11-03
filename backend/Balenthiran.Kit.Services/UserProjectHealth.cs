using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-WELCOME-1: a user's project's test health, counted as BEH-HEALTH-1 counts the operator's. Each
/// <c>x.beh</c> is mapped by the <c>x.tests.json</c> beside it, and its tests are the repo's own, so
/// every entry must name a test that exists in that repo. Every way of not knowing says which.
/// </summary>
public static class UserProjectHealth
{
    /// <summary>The files a count reads: the corpus, its mappings and the repo's tests.</summary>
    public static bool Wanted(string path, ICoverageCounter counter) =>
        path.EndsWith(".beh", StringComparison.Ordinal)
        || path.EndsWith(".tests.json", StringComparison.Ordinal)
        || counter.IsTestPath(path);

    /// <summary>The share of <paramref name="files"/>' behaviours a test names, pending ones left out as kit#155 does.</summary>
    public static CoverageSummary Count(IReadOnlyDictionary<string, string> files, ICorpusParser parser, IBehaviourResolver resolver, ICoverageCounter counter)
    {
        var corpora = files.Keys.Where(p => p.EndsWith(".beh", StringComparison.Ordinal)).ToList();
        if (corpora.Count == 0)
        {
            return Unavailable("no .beh file on the default branch");
        }

        var reading = counter.ReadTitles(files.Where(f => counter.IsTestPath(f.Key)).Select(f => (f.Key, f.Value)));
        if (reading.Refusal is { } refusal)
        {
            return Unavailable(refusal);
        }

        int covered = 0, uncovered = 0;
        foreach (var path in corpora)
        {
            var mapPath = path[..^".beh".Length] + ".tests.json";
            if (!files.TryGetValue(mapPath, out var tests))
            {
                return Unavailable($"no {mapPath} mapping");
            }

            JsonObject map;
            try
            {
                map = JsonNode.Parse(tests.TrimStart('﻿')) as JsonObject ?? throw new JsonException("not an object");
            }
            catch (JsonException)
            {
                return Unavailable($"{mapPath} is not a JSON object");
            }

            IReadOnlyList<Abstractions.DataModels.IBehaviour> behaviours;
            try
            {
                behaviours = resolver.Resolve(parser.Parse(files[path], Path.GetFileName(path))).Behaviours;
            }
            catch (CorpusParseException e)
            {
                return Unavailable($"{path}: {e.Message}");
            }

            var counted = counter.Count(behaviours, map, reading.Titles, mapPath);
            if (counted.Refusal is { } no)
            {
                return Unavailable(no);
            }

            covered += counted.Covered.Count;
            uncovered += counted.Uncovered.Count;
        }

        return new CoverageSummary { Available = true, Covered = covered, Uncovered = uncovered, Reason = null };
    }

    private static CoverageSummary Unavailable(string reason) => new() { Available = false, Covered = null, Uncovered = null, Reason = reason };
}
