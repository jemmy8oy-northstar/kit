using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The <c>resolve</c> branches no committed golden reaches, scored against Node's
/// output for <c>Fixtures/resolve-edges.beh</c> (written by <c>conformance.js</c>'s
/// own <c>pipeline</c>; the corpus's header says how to regenerate it). Without
/// this, the unowned-hole branch — a different KEY ORDER from the owned one — would
/// be ported, compiled and never once compared with anything.
/// </summary>
public class ResolveEdgeTests
{
    private readonly ResolveScorer _scorer = new(new CorpusParser(), new BehaviourResolver(), new EngineJsonSerialiser());

    [Fact]
    public void Resolve_reproduces_Node_on_the_branches_the_goldens_miss()
    {
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Fixtures, "resolve-edges.json")))!;
        var expected = golden["resolve"]!;

        // The fixture is only worth having if it still holds what it was built for —
        // asserted on Node's side, so a regenerated fixture that lost a branch fails.
        var expectedText = expected.ToJsonString();
        Assert.Contains("\"key\":\"?count\"", expectedText, StringComparison.Ordinal);
        Assert.Contains("\"contributors\":[\"BEH-A\",\"BEH-B\",\"BEH-B\"]", expectedText, StringComparison.Ordinal);
        Assert.Single(expected["conflicts"]!.AsArray());

        var actual = _scorer.Section(File.ReadAllText(Path.Combine(RepoLayout.Fixtures, "resolve-edges.beh")), "resolve-edges.beh");

        Assert.Equal(_scorer.Canonical(expected), _scorer.Canonical(actual));
    }

    [Fact]
    public void An_unowned_hole_writes_slot_before_key_and_an_owned_one_key_before_slot()
    {
        // Pinned by NAME as well as by the fixture, because it is the one place in
        // the port where two shapes share an interface and differ only in order.
        var parsed = new CorpusParser().Parse("behaviour BEH-A \"a\"\n  when types \"x\" with ?n\n  then sees field:F with ?v\n", "t.beh");
        var resolution = new BehaviourResolver().Resolve(parsed);
        var open = JsonNode.Parse(new EngineJsonSerialiser().Serialise(resolution.Behaviours[0]))!["open"]!.AsArray();

        Assert.Equal("{\"slot\":\"n\",\"key\":\"?n\",\"at\":\"t.beh:2\"}", open[0]!.ToJsonString());
        Assert.Equal("{\"key\":\"field:F.v\",\"slot\":\"v\",\"at\":\"t.beh:3\"}", open[1]!.ToJsonString());
    }

    [Fact]
    public void A_behaviour_the_parser_did_not_build_is_refused_not_skipped()
    {
        var foreign = new ForeignBehaviour();
        var ex = Assert.Throws<ArgumentException>(() => new BehaviourResolver().Resolve([foreign]));
        Assert.Contains("BEH-FOREIGN", ex.Message, StringComparison.Ordinal);
    }

    private sealed class ForeignBehaviour : IBehaviour
    {
        public string Id => "BEH-FOREIGN";

        public string Title => "not from the parser";

        public string? Actor => null;

        public IReadOnlyList<IStep> Steps => [];

        public IReadOnlyList<IUnknown> Unknowns => [];

        public IReadOnlyList<IProvide> Provides => [];

        public IReadOnlyList<IIdRef> Serves => [];

        public string At => "x:1";

        public string? Asks => null;

        public IReadOnlyList<IOption> Options => [];

        public IRecommendation? Recommend => null;

        public string? Against => null;

        public IReadOnlyList<IIdRef> Cites => [];

        public ISource Source => null!;

        public IReview Review => null!;

        public bool Pending => false;

        public string Layer => "ux";

        public bool? ReviewExplicit => null;

        public IReadOnlyList<IFilled>? Filled => null;

        public IReadOnlyList<IOpenHole>? Open => null;
    }
}
