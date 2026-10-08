using System.Text.Json.Nodes;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The <c>resolve</c> port's score: every committed golden's <c>resolve</c> section,
/// reproduced from the C# parser + resolver. Same shape as the <c>parse</c> test in
/// <see cref="ConformanceTests"/>, and the same rule — Node is the specification.
/// </summary>
public class ResolveConformanceTests
{
    private readonly ResolveScorer _scorer = new(new CorpusParser(), new BehaviourResolver(), new EngineJsonSerialiser());

    public static TheoryData<string> Corpora() => ConformanceTests.Corpora();

    [Theory]
    [MemberData(nameof(Corpora))]
    public void Resolve_reproduces_the_golden_section(string corpus)
    {
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Conformance, $"{corpus}.json")))!;
        var expected = golden["resolve"]!;

        // 🔑 Vacuity guard: `changed` has one entry per behaviour, so an empty one
        // means a resolver that returned nothing would compare equal.
        Assert.True(expected["changed"]!.AsArray().Count > 0, $"golden for {corpus} has an EMPTY resolve.changed");

        var actual = _scorer.Section(File.ReadAllText(Path.Combine(RepoLayout.Behaviours, $"{corpus}.beh")), $"{corpus}.beh");

        // Section by section first, so a failure names the half that is wrong
        // rather than dumping two whole documents.
        foreach (var part in new[] { "symbols", "conflicts", "changed" })
        {
            Assert.Equal(_scorer.Canonical(expected[part]), _scorer.Canonical(actual[part]));
        }

        Assert.Equal(_scorer.Canonical(expected), _scorer.Canonical(actual));
    }

    /// <summary>
    /// The eleven goldens are only a score for the branches they EXERCISE, so that is
    /// measured rather than assumed. If a corpus edit ever removed the last resolved
    /// step or the last conflict, the theory above would stay green while scoring
    /// nothing about that branch — so this goes red instead, and points at the
    /// edge fixture as the place that branch is still held.
    /// </summary>
    [Fact]
    public void The_goldens_exercise_a_fill_a_conflict_and_an_owned_open_hole()
    {
        int resolved = 0, conflicts = 0, owned = 0;
        foreach (var f in Directory.GetFiles(RepoLayout.Conformance, "*.json"))
        {
            var r = JsonNode.Parse(File.ReadAllText(f))!["resolve"]!;
            conflicts += r["conflicts"]!.AsArray().Count;
            foreach (var c in r["changed"]!.AsArray().SelectMany(b => b!["changes"]!.AsArray()))
            {
                var path = c!["path"]!.GetValue<string>();
                if (path.EndsWith(".resolved", StringComparison.Ordinal))
                {
                    resolved++;
                }

                if (path == "open")
                {
                    owned += c["to"]!.AsArray().Count(o => !o!["key"]!.GetValue<string>().StartsWith('?'));
                }
            }
        }

        Assert.True(resolved > 0, "no golden has a resolved step any more");
        Assert.True(conflicts > 0, "no golden has a conflict any more");
        Assert.True(owned > 0, "no golden has an owned open hole any more");
    }
}
