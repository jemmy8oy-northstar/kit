using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The project view's <c>report</c> section — adjudication, surface, the question
/// sheet and requires — reproduced from the C# parser + resolver + reporter, on every
/// golden and on <c>Fixtures/report-edges</c>, which reaches what no golden does.
/// </summary>
public class ReportConformanceTests
{
    private readonly EngineJsonSerialiser _serialiser = new();

    public static TheoryData<string> Corpora() => ConformanceTests.Corpora();

    [Theory]
    [MemberData(nameof(Corpora))]
    public void Report_reproduces_the_golden_section(string corpus) =>
        AssertSection(RepoLayout.Conformance, RepoLayout.FrozenBehaviours, corpus);

    [Fact]
    public void Report_reproduces_Node_on_the_branches_the_goldens_miss() =>
        AssertSection(RepoLayout.Fixtures, RepoLayout.Fixtures, "report-edges");

    /// <summary>
    /// The edge corpus still holds what it was built for, asserted on Node's output —
    /// and the one property no golden can hold: that the noun list is in LOCALE order,
    /// which here differs from ordinal. A golden where the two agree scores nothing
    /// about it.
    /// </summary>
    [Fact]
    public void The_edge_corpus_still_reaches_its_branches()
    {
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Fixtures, "report-edges.json")))!["report"]!;
        var text = report.ToJsonString();

        Assert.Equal(4, report["surface"]!["errors"]!.AsArray().Count);
        Assert.Single(report["adjudication"]!["approved"]!.AsArray());
        Assert.Single(report["adjudication"]!["denied"]!.AsArray());
        Assert.NotEmpty(report["requires"]!["insufficient"]!.AsArray());
        Assert.Contains("\"owner\":null", text, StringComparison.Ordinal);
        Assert.Contains("\"title\":\"BEH-NOWHERE\"", text, StringComparison.Ordinal);

        var nouns = report["requires"]!["nouns"]!.AsArray().Select(n => n!["noun"]!.GetValue<string>()).ToList();
        Assert.NotEqual(nouns.Order(StringComparer.Ordinal), nouns);
    }

    /// <summary>
    /// Under invariant-globalization mode, invariant-culture comparison is ORDINAL, and
    /// the noun order silently changes. That mode is the default in some .NET container
    /// images — so the environment itself is asserted, and the message names the cause.
    /// </summary>
    [Fact]
    public void Globalization_is_ICU_so_locale_order_is_reachable() =>
        Assert.True(
            string.Compare("field:name", "field:Name", CultureInfo.InvariantCulture, CompareOptions.None) < 0,
            "invariant-culture comparison is ordinal here — DOTNET_SYSTEM_GLOBALIZATION_INVARIANT is on, "
            + "or ICU is missing, and the requires noun list will not match Node's localeCompare");

    private void AssertSection(string goldenDir, string corpusDir, string corpus)
    {
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(goldenDir, $"{corpus}.json")))!["report"]!;

        var parser = new CorpusParser();
        var parsed = parser.Parse(File.ReadAllText(Path.Combine(corpusDir, $"{corpus}.beh")), $"{corpus}.beh");
        var resolution = new BehaviourResolver().Resolve(parsed);
        var report = new ProjectReporter().Report(resolution.Behaviours, resolution.Conflicts, GenerateScorer.Bindings(corpusDir, corpus));
        var actual = JsonNode.Parse(_serialiser.Serialise(report))!;

        // Part by part first, so a failure names the function that differs.
        foreach (var part in new[] { "adjudication", "surface", "questions", "requires" })
        {
            Assert.Equal(Canonical(expected[part]), Canonical(actual[part]));
        }

        Assert.Equal(Canonical(expected), Canonical(actual));
    }

    private string Canonical(JsonNode? node) => JsonSerializer.Serialize(node, _serialiser.Options);
}
