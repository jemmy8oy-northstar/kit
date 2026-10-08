using System.Text.Json.Nodes;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c>conformance.js</c>'s <c>delta</c>, ported so the <c>resolve</c> section of a
/// golden can be scored at all: the golden does not hold resolved behaviours, it
/// holds the structural diff between each one and its <c>parse</c> snapshot. A C#
/// port that cannot compute the same diff cannot be compared with it.
///
/// ⚠️ This is the scorer, so a bug here is a bug in the measurement. The rules are
/// copied, not improved:
/// <list type="bullet">
/// <item>A MISSING key is not JSON <c>null</c> — JavaScript's <c>undefined</c> vs
/// <c>null</c>. <c>JSON.stringify</c> distinguishes them, so a key present as null
/// on one side and absent on the other IS a change, reported <c>null → null</c>.</item>
/// <item>A missing side against an object reports the WHOLE object once
/// (<c>prim(undefined)</c> is true), rather than one entry per field — which is
/// why every behaviour's <c>filled</c> is a single change.</item>
/// <item>Keys are walked <c>before</c>'s first, then those only <c>after</c> has, in
/// insertion order and never sorted. Array indices are keys.</item>
/// </list>
/// </summary>
internal static class JsonDelta
{
    internal static JsonArray Of(JsonNode? before, JsonNode? after)
    {
        var output = new JsonArray();
        Walk(new Side(true, before), new Side(true, after), string.Empty, output);
        return output;
    }

    private static void Walk(Side before, Side after, string at, JsonArray output)
    {
        if (before.IsPrimitive || after.IsPrimitive)
        {
            if (before.Stringified != after.Stringified)
            {
                output.Add(Change(at, before, after));
            }

            return;
        }

        if ((before.Node is JsonArray) != (after.Node is JsonArray))
        {
            output.Add(Change(at, before, after));
            return;
        }

        var beforeKeys = KeysOf(before.Node!);
        var keys = beforeKeys.Concat(KeysOf(after.Node!).Where(k => !beforeKeys.Contains(k)));
        foreach (var k in keys)
        {
            Walk(Child(before.Node!, k), Child(after.Node!, k), at.Length > 0 ? $"{at}.{k}" : k, output);
        }
    }

    private static JsonObject Change(string at, Side before, Side after) => new()
    {
        ["path"] = at,
        ["from"] = before.Node?.DeepClone(),
        ["to"] = after.Node?.DeepClone(),
    };

    private static List<string> KeysOf(JsonNode node) => node switch
    {
        JsonObject o => o.Select(kv => kv.Key).ToList(),
        JsonArray a => Enumerable.Range(0, a.Count).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList(),
        _ => [],
    };

    private static Side Child(JsonNode node, string key) => node switch
    {
        JsonObject o => o.TryGetPropertyValue(key, out var v) ? new Side(true, v) : new Side(false, null),
        JsonArray a => int.Parse(key, System.Globalization.CultureInfo.InvariantCulture) is var i && i < a.Count
            ? new Side(true, a[i])
            : new Side(false, null),
        _ => new Side(false, null),
    };

    /// <summary>One side of a comparison. <see cref="Present"/> false is <c>undefined</c>.</summary>
    private readonly record struct Side(bool Present, JsonNode? Node)
    {
        public bool IsPrimitive => !Present || Node is null or JsonValue;

        /// <summary><c>JSON.stringify</c>'s answer: no string at all for undefined.</summary>
        public string? Stringified => !Present ? null : Node?.ToJsonString() ?? "null";
    }
}
