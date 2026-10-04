using System.Text.Json;
using System.Text.Json.Nodes;
using Kit.Engine;

namespace Kit.Tests;

/// <summary>
/// The port's score, not its review.
///
/// Every corpus in <c>prototypes/behaviour-ast/behaviours/</c> has a committed
/// golden holding what the Node engine produces for it (kit#116). This drives the
/// C# <see cref="Parser"/> over the same corpus and asserts the bytes match the
/// golden's <c>parse</c> section exactly. The Node engine is the specification;
/// a difference is this port being wrong, not the golden being stale.
///
/// ⚠️ Only the <c>parse</c> section is asserted here, because only <c>parse</c>
/// has been ported. That is the whole reason kit#116 sectioned the goldens by
/// engine stage: a single opaque blob per corpus could not score a half-done
/// port, so the first C# module would have been unverifiable until the last one
/// existed — the same as having no harness at all.
/// </summary>
public class ConformanceTests
{
    /// <summary>
    /// Both sides are put through ONE serialiser, so a formatting difference
    /// cannot be mistaken for a parsing difference.
    ///
    /// The golden's <c>parse</c> section is re-serialised from its
    /// <see cref="JsonNode"/> rather than compared as raw substring bytes,
    /// because the golden file holds it indented one level deeper (it is nested
    /// inside the document) — comparing the file's own bytes would fail on
    /// leading whitespace for every corpus and prove nothing about the parser.
    /// </summary>
    private static string Canonical(JsonNode? node) =>
        JsonSerializer.Serialize(node, EngineJson.Options);

    public static TheoryData<string> Corpora()
    {
        var data = new TheoryData<string>();
        foreach (var f in Directory.GetFiles(RepoLayout.Behaviours, "*.beh"))
        {
            data.Add(Path.GetFileNameWithoutExtension(f));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Corpora))]
    public void Parse_reproduces_the_golden_byte_for_byte(string corpus)
    {
        var behPath = Path.Combine(RepoLayout.Behaviours, $"{corpus}.beh");
        var goldenPath = Path.Combine(RepoLayout.Conformance, $"{corpus}.json");

        Assert.True(File.Exists(goldenPath), $"no golden for corpus {corpus} at {goldenPath}");

        var golden = JsonNode.Parse(File.ReadAllText(goldenPath))!.AsObject();
        var expectedNode = golden["parse"];
        Assert.NotNull(expectedNode);

        // 🔑 Vacuity guard. An empty expectation would make this test pass for a
        // parser that returned nothing, which is the failure mode a golden-file
        // harness is most prone to: the comparison runs, both sides are empty,
        // and the suite reports green over a parser that does not work.
        var expectedCount = expectedNode!.AsArray().Count;
        Assert.True(expectedCount > 0, $"golden for {corpus} has an EMPTY parse section — this test would pass vacuously");

        var actual = Parser.Parse(File.ReadAllText(behPath), $"{corpus}.beh");
        Assert.Equal(expectedCount, actual.Count);

        Assert.Equal(Canonical(expectedNode), Canonical(JsonSerializer.SerializeToNode(actual, EngineJson.Options)));
    }

    /// <summary>
    /// The population itself is asserted, because the test above can only fail
    /// for a corpus it is given. If <see cref="Corpora"/> silently returned
    /// nothing — a moved directory, a changed extension — xunit reports zero
    /// failures, and zero failures is what green looks like.
    /// </summary>
    [Fact]
    public void Every_corpus_has_a_golden_and_the_population_is_not_empty()
    {
        var corpora = Directory.GetFiles(RepoLayout.Behaviours, "*.beh")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        var goldens = Directory.GetFiles(RepoLayout.Conformance, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(corpora);
        Assert.Equal(goldens, corpora);
    }
}
