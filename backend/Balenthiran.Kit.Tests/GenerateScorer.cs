using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// Builds the C# side of a golden's <c>generate</c> section exactly as
/// <c>conformance.js</c>'s <c>pipeline</c> builds the Node side: parse, resolve
/// (which writes the fields a <c>fills</c> generates from), then one
/// <c>{ id, code, missing, stats }</c> per behaviour against the corpus's OWN
/// bindings. Shared by the goldens and the edge fixtures so they score alike.
/// </summary>
internal sealed class GenerateScorer(ICorpusParser parser, IBehaviourResolver resolver, ITestGenerator generator, IEngineJsonSerialiser serialiser)
{
    /// <summary>The section for corpus <paramref name="corpus"/> in <paramref name="dir"/>.</summary>
    internal JsonArray Section(string dir, string corpus)
    {
        var parsed = parser.Parse(File.ReadAllText(Path.Combine(dir, $"{corpus}.beh")), $"{corpus}.beh");
        var resolution = resolver.Resolve(parsed);
        var symbols = resolution.Symbols.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        var bindings = Bindings(dir, corpus);

        var section = new JsonArray();
        foreach (var b in resolution.Behaviours)
        {
            var test = JsonNode.Parse(serialiser.Serialise(generator.Generate(b, bindings, symbols)))!.AsObject();
            var entry = new JsonObject { ["id"] = b.Id };
            foreach (var (key, value) in test.ToList())
            {
                test.Remove(key);
                entry[key] = value;
            }

            section.Add(entry);
        }

        return section;
    }

    internal string Canonical(JsonNode? node) => JsonSerializer.Serialize(node, serialiser.Options);

    /// <summary>
    /// <c>bindings.js</c>'s <c>readFor</c>: the file beside the corpus, verbatim;
    /// no file binds nothing, which is a real state rather than an error.
    /// </summary>
    internal static JsonObject Bindings(string dir, string corpus)
    {
        var file = Path.Combine(dir, $"{corpus}.bindings.json");
        return File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file))!.AsObject() : [];
    }
}
