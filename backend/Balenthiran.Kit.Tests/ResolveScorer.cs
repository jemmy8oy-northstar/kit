using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// Builds the C# side of a golden's <c>resolve</c> section, exactly as
/// <c>conformance.js</c>'s <c>pipeline</c> builds the Node side:
/// <c>{ symbols: [[key, symbol]...], conflicts, changed: [{ id, changes }] }</c>.
/// Shared by the eleven-corpus conformance test and the edge fixture, so the two
/// cannot score by different rules.
/// </summary>
internal sealed class ResolveScorer(ICorpusParser parser, IBehaviourResolver resolver, IEngineJsonSerialiser serialiser)
{
    internal JsonObject Section(string text, string file)
    {
        var parsed = parser.Parse(text, file);

        // Snapshot BEFORE resolve, which mutates the parsed behaviours in place —
        // conformance.js:161, for the same reason.
        var parseSnapshot = Node(parsed).AsArray();

        var resolution = resolver.Resolve(parsed);

        var symbols = new JsonArray();
        foreach (var (key, symbol) in resolution.Symbols)
        {
            symbols.Add(new JsonArray(JsonValue.Create(key), Node(symbol)));
        }

        var changed = new JsonArray();
        for (var i = 0; i < resolution.Behaviours.Count; i++)
        {
            var b = resolution.Behaviours[i];
            changed.Add(new JsonObject
            {
                ["id"] = b.Id,
                ["changes"] = JsonDelta.Of(parseSnapshot[i], Node(b)),
            });
        }

        return new JsonObject
        {
            ["symbols"] = symbols,
            ["conflicts"] = Node(resolution.Conflicts),
            ["changed"] = changed,
        };
    }

    /// <summary>Through the engine's own serialiser, by RUNTIME type — the wire shape, not the interface's.</summary>
    private JsonNode Node(object value) => JsonNode.Parse(serialiser.Serialise(value))!;

    internal string Canonical(JsonNode? node) => JsonSerializer.Serialize(node, serialiser.Options);
}
