using System.Text.Json.Nodes;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-HEALTH-4: the one place a test link's <c>"verified"</c> record is compared with the files on
/// disk. <c>kit verify-links check</c> and the Health page's flagged list both read it, so the two
/// cannot disagree about which links are possibly broken.
/// </summary>
public static class LinkVerification
{
    /// <summary>Every link object under a behaviour id in a parsed <c>tests.json</c>; "_"-keys are the file's comments.</summary>
    public static IEnumerable<(string Id, JsonObject Link)> Links(JsonObject map)
    {
        foreach (var (id, node) in map)
        {
            if (id.StartsWith('_'))
            {
                continue;
            }

            var entries = node is JsonArray many ? many.OfType<JsonObject>() : node is JsonObject one ? [one] : [];
            foreach (var link in entries)
            {
                yield return (id, link);
            }
        }
    }

    /// <summary>A string property of a link, or null when absent or not a string.</summary>
    public static string? Text(JsonObject o, string key) =>
        o.TryGetPropertyValue(key, out var v) && v is JsonValue s && s.TryGetValue<string>(out var t) ? t : null;

    /// <summary>
    /// A link's recorded paths. An entry that is not a string, or that would leave the checkout (rooted, or
    /// through ".."), reads as "", which no file is: it shows as gone instead of throwing or reading outside.
    /// </summary>
    public static List<string> Files(JsonArray files) =>
        files.Select(f => f is JsonValue s && s.TryGetValue<string>(out var t) && !Path.IsPathRooted(t) && !t.Split('/', '\\').Contains("..") ? t : string.Empty).ToList();

    /// <summary>
    /// Compare one link with the source under <paramref name="repo"/>. A link with no complete
    /// <c>verified</c> record is unrecorded; a file that is gone, or a hash that differs, is possibly broken.
    /// </summary>
    public static LinkVerdict Classify(JsonObject link, string repo)
    {
        if (link["verified"] is not JsonObject v || Text(v, "hash") is not { } recorded || v["files"] is not JsonArray files)
        {
            return new LinkVerdict(LinkState.Unrecorded);
        }

        var paths = Files(files);
        var gone = paths.FirstOrDefault(p => !File.Exists(Path.Combine(repo, p)));
        if (gone is not null)
        {
            return new LinkVerdict(LinkState.PossiblyBroken, $"{gone} is gone");
        }

        return LinkHasher.Hash(repo, paths) != recorded
            ? new LinkVerdict(LinkState.PossiblyBroken, $"the code it exercises has changed since {Text(v, "on")}")
            : new LinkVerdict(LinkState.Verified);
    }
}
