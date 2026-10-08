using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The writer's score: every call in <c>conformance/routes/writes.json</c>'s
/// <c>functions</c>, which <c>conformance.js</c> records from <c>writer.js</c> over the
/// fixture corpora in <c>conformance/writes/</c>, answered by <see cref="CorpusWriter"/> —
/// the whole text after each edit, byte for byte, or the same refusal.
/// </summary>
public class WritesConformanceTests
{
    private static readonly EngineJsonSerialiser Serialiser = new();

    private static string Fixtures => Path.Combine(RepoLayout.Conformance, "writes");

    public static TheoryData<int> Calls()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < Golden()["functions"]!.AsArray().Count; i++)
        {
            data.Add(i);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void Every_edit_matches_writer_js(int index)
    {
        var call = Golden()["functions"]![index]!;
        var fn = call["fn"]!.GetValue<string>();
        var text = Texts()[call["input"]!.GetValue<string>()];
        var args = call["args"]!.AsArray();
        var writer = new CorpusWriter(new CorpusParser());

        IWriteResult r = fn switch
        {
            "addStep" => writer.AddStep(text, Str(args[0])!, Str(args[1])!),
            "addBehaviour" => writer.AddBehaviour(
                text,
                Str(args[0])!,
                Str(args[1])!,
                Str(args[2]!["actor"]),
                args[2]!["steps"]?.AsArray().Select(s => s!.GetValue<string>()).ToList(),
                Str(args[2]!["source"]),
                Str(args[2]!["ref"])),
            "setReview" => writer.SetReview(text, Str(args[0])!, Str(args[1])!, Str(args[2])),
            "addBinding" => writer.AddBinding(text, Str(args[0])!, JsonSerializer.SerializeToElement(args[1]), Nouns(writer), "alpha"),
            _ => throw new InvalidOperationException(fn),
        };

        var expected = call["result"]!.AsObject();
        var label = $"{fn} {call["input"]} {args.ToJsonString()}";
        Assert.True(expected["ok"]!.GetValue<bool>() == r.Ok, $"{label}: ok {r.Ok} ({r.Error}: {r.Reason})");
        Assert.Equal(Canonical(expected), Canonical(Actual(r)));
    }

    /// <summary>
    /// The routes, end to end through <see cref="KitHost"/> against a temporary COPY of the
    /// fixtures, in the golden's order — each response, and the exact text of each file a
    /// 200 wrote. A write's <c>file</c> names the copy, so it is compared as <c>&lt;dir&gt;/name</c>.
    /// </summary>
    [Fact]
    public void Every_route_answers_and_writes_as_ui_js_does()
    {
        var tmp = Directory.CreateTempSubdirectory("kit-writes-").FullName;
        try
        {
            foreach (var f in Directory.GetFiles(Fixtures))
            {
                File.Copy(f, Path.Combine(tmp, Path.GetFileName(f)));
            }

            var corpora = new Balenthiran.Kit.Database.CorpusDirectory(tmp, RepoLayout.Root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            var policy = new OriginPolicy(new UrlParser(), null);
            var host = new KitHost(new KitRouter(corpora, viewer, new UiBundle(tmp), null, policy), new UrlParser(), Serialiser, policy, string.Empty);
            var relTmp = Path.GetRelativePath(RepoLayout.Root, tmp).Replace('\\', '/');

            var steps = Golden()["routes"]!.AsArray();
            Assert.True(steps.Count >= 20, $"only {steps.Count} route steps — the gate is inert");
            foreach (var s in steps)
            {
                var body = s!["body"]!;
                var raw = body.GetValueKind() == JsonValueKind.String ? body.GetValue<string>() : body.ToJsonString();
                var label = $"{s["path"]} {raw}";
                var a = host.Received(s["path"]!.GetValue<string>(), System.Text.Encoding.UTF8.GetBytes(raw), null, null);

                var actual = JsonNode.Parse(a.Body!)!.AsObject();
                if (actual["file"]?.GetValue<string>() is { } file && file.StartsWith(relTmp + "/", StringComparison.Ordinal))
                {
                    actual["file"] = "<dir>/" + file[(relTmp.Length + 1)..];
                }

                var expected = s["response"]!;
                Assert.True(expected["status"]!.GetValue<int>() == a.Status, $"{label}: status {a.Status}, body {a.Body}");
                Assert.Equal(Canonical(expected["body"]), Canonical(actual));
                if (s["written"] is JsonObject written)
                {
                    Assert.Equal(written["text"]!.GetValue<string>(), Bytes(Path.Combine(tmp, written["name"]!.GetValue<string>())));
                }
            }
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }

    /// <summary>The golden reaches every refusal code — the C# side's own count, beside kit.test.js's.</summary>
    [Fact]
    public void The_golden_reaches_every_refusal()
    {
        var codes = Golden()["functions"]!.AsArray().Select(f => f!["result"]!["error"]?.GetValue<string>()).Where(e => e is not null).ToHashSet();
        Assert.True(codes.Count >= 14, $"only {codes.Count} refusal codes: {string.Join(", ", codes)}");
    }

    /// <summary>The recorder's named inputs, derived exactly as <c>conformance.js</c> derives them.</summary>
    private static Dictionary<string, string> Texts()
    {
        string Read(string f) => Bytes(Path.Combine(Fixtures, f));
        return new Dictionary<string, string>
        {
            ["alpha"] = Read("alpha.beh"),
            ["broken"] = Read("broken.beh"),
            ["alpha-unterminated"] = Read("alpha.beh").TrimEnd(), // the fixture ends in newlines only, where .NET and JS whitespace agree
            ["bindings"] = Read("alpha.bindings.json"),
            ["bindings-empty"] = "{}",
            ["bindings-array"] = "[]",
            ["bindings-bad"] = "{",
            ["indented"] = Read("indented.txt"),
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Nouns(CorpusWriter writer)
    {
        var corpora = Directory.GetFiles(Fixtures, "*.beh").OrderBy(f => f, StringComparer.Ordinal)
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f), Bytes);
        return writer.CorpusNouns(corpora, []);
    }

    /// <summary>A file's text with any BOM KEPT — <c>File.ReadAllText</c> would hide the very thing one fixture tests.</summary>
    private static string Bytes(string path) => new System.Text.UTF8Encoding(false).GetString(File.ReadAllBytes(path));

    /// <summary>The result as <c>writer.js</c> returns it: only the fields it sets, in its order.</summary>
    private static JsonObject Actual(IWriteResult r)
    {
        var o = new JsonObject { ["ok"] = r.Ok };
        if (r.Ok)
        {
            o["text"] = r.Text;
            if (r.Noun is not null)
            {
                o["noun"] = r.Noun;
                o["sharedWith"] = new JsonArray(r.SharedWith!.Select(s => (JsonNode)s).ToArray());
            }
        }
        else
        {
            o["error"] = r.Error;
            o["reason"] = r.Reason;
            if (r.Known is not null)
            {
                o["known"] = new JsonArray(r.Known.Select(s => (JsonNode)s).ToArray());
            }

            if (r.Current is not null)
            {
                o["current"] = r.Current.DeepClone();
            }
        }

        return o;
    }

    private static string? Str(JsonNode? n) => n?.GetValue<string>();

    private static JsonNode Golden() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Conformance, "routes", "writes.json")))!;

    private static string Canonical(JsonNode? node) => JsonSerializer.Serialize(node, Serialiser.Options);
}
