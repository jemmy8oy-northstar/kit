using System.Text.Json.Nodes;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The <c>generate</c> port's score: every committed golden's <c>generate</c> section,
/// and the two edge corpora in <c>Fixtures/</c> that reach what no golden does —
/// all reproduced from the C# parser + resolver + generator. Node is the specification.
/// </summary>
public class GenerateConformanceTests
{
    private readonly GenerateScorer _scorer = new(new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new EngineJsonSerialiser());

    public static TheoryData<string> Corpora() => ConformanceTests.Corpora();

    public static TheoryData<string> EdgeCorpora() => new() { "generate-edges", "generate-nofixture" };

    [Theory]
    [MemberData(nameof(Corpora))]
    public void Generate_reproduces_the_golden_section(string corpus) =>
        AssertSection(RepoLayout.Conformance, RepoLayout.Behaviours, corpus);

    [Theory]
    [MemberData(nameof(EdgeCorpora))]
    public void Generate_reproduces_Node_on_the_branches_the_goldens_miss(string corpus) =>
        AssertSection(RepoLayout.Fixtures, RepoLayout.Fixtures, corpus);

    /// <summary>
    /// The goldens are only a score for the branches they EXERCISE, so that is
    /// measured, on Node's side, rather than assumed — a corpus edit that removed
    /// the last filled route or the last resolved fill would otherwise leave the
    /// theory green while scoring nothing about it.
    /// </summary>
    [Fact]
    public void The_goldens_exercise_a_filled_route_a_resolved_fill_an_attach_and_a_refusal()
    {
        var code = string.Join("\n", Directory.GetFiles(RepoLayout.Conformance, "*.json")
            .SelectMany(f => JsonNode.Parse(File.ReadAllText(f))!["generate"]!.AsArray())
            .Select(g => g!["code"]!.GetValue<string>()));

        Assert.Contains("page.goto(\"./editor/11111111-", code, StringComparison.Ordinal);
        Assert.Contains(".setInputFiles({\"name\":\"talk.mp4\"", code, StringComparison.Ordinal);
        Assert.Contains("exact: true", code, StringComparison.Ordinal);
        Assert.Contains("type: \"kit-ungenerated\"", code, StringComparison.Ordinal);
        Assert.Contains("// CONTRACT (not derivable from a behaviour):", code, StringComparison.Ordinal);
    }

    /// <summary>The edge corpora still hold what they were built for — asserted on Node's output.</summary>
    [Fact]
    public void The_edge_corpora_still_reach_their_branches()
    {
        var edges = File.ReadAllText(Path.Combine(RepoLayout.Fixtures, "generate-edges.json"));
        foreach (var held in new[]
        {
            "page.goto(\\\"/d/x:ay$z/d/w/:av$1/:b\\\")",
            "{ name: undefined }",
            "page.42",
            "\\\\u0001",
            "await 1e+21;",
            "await 1,a,,2,3;",
            "await [object Object];",
            "setInputFiles({\\\"1\\\":[1,0,0.000001,123456789012345680000],\\\"2\\\":true,\\\"b\\\":1,\\\"a\\\":null})",
            "\"region:Null\"",
        })
        {
            Assert.Contains(held, edges, StringComparison.Ordinal);
        }

        Assert.Contains(".fill('')", File.ReadAllText(Path.Combine(RepoLayout.Fixtures, "generate-nofixture.json")), StringComparison.Ordinal);
    }

    /// <summary>
    /// The one KNOWN divergence from Node, pinned so it stays loud. A bindings file may
    /// spell a lone surrogate (<c>"\ud800"</c>); <c>JSON.parse</c> keeps it and
    /// <c>JSON.stringify</c> writes it back escaped, but System.Text.Json cannot hold
    /// one in a string at all. The port must THROW there — could-not-read — never
    /// write a different test. Nothing else can carry one in: corpus text arrives as
    /// UTF-8, which cannot encode a lone surrogate, in Node or here.
    /// </summary>
    [Fact]
    public void A_lone_surrogate_in_a_binding_is_refused_loudly_not_rewritten()
    {
        var parsed = new CorpusParser().Parse("behaviour BEH-A \"a\"\n  then sees field:F\n", "t.beh");
        var bindings = JsonNode.Parse("{\"field:F\": {\"label\": \"x\\ud800\"}}")!.AsObject();

        Assert.Throws<InvalidOperationException>(() =>
            new TestGenerator().Generate(parsed[0], bindings, new Dictionary<string, Abstractions.DataModels.ISymbol>()));
    }

    [Fact]
    public void The_annotation_type_is_the_string_tooling_reads() =>
        Assert.Equal("kit-ungenerated", TestGenerator.UngeneratedAnnotation);

    private void AssertSection(string goldenDir, string corpusDir, string corpus)
    {
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(goldenDir, $"{corpus}.json")))!["generate"]!.AsArray();

        // 🔑 Vacuity guard: one entry per behaviour, so an empty one would compare
        // equal to a generator that produced nothing.
        Assert.True(expected.Count > 0, $"golden for {corpus} has an EMPTY generate section");

        var actual = _scorer.Section(corpusDir, corpus);
        Assert.Equal(expected.Count, actual.Count);

        // Behaviour by behaviour first, so a failure names the test that differs.
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(_scorer.Canonical(expected[i]), _scorer.Canonical(actual[i]));
        }
    }
}
