using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-HEALTH-1, the backend half: coverage counted once for the gate and for hosted Kit, a
/// <c># kit:repo</c> line saying where the tests live, and a project view that says WHY when it
/// cannot give a number. (The health page itself is a later slice, so the behaviour stays pending.)
/// </summary>
public class CoverageTests
{
    private const string Repo = "o/r@dev";

    private const string Tests = "public class ThingTests\n{\n    [Fact]\n    public void It_works() { }\n}\n";

    private const string Corpus =
        "# kit:repo o/r@dev\nbehaviour BEH-1 \"a thing\"\n  when opens page:Home\n\nbehaviour BEH-2 \"another\"\n  when opens page:Home\n\nbehaviour BEH-3 \"later\"\n  when opens page:Home\n  pending\n";

    private const string Mapping = """{"BEH-1": [{"file": "ThingTests.cs", "title": "It_works"}]}""";

    private static readonly CoverageCounter Counter = new(new TestTitleReader());

    private static ITestReading Reading(string src = Tests) => Counter.ReadTitles([("ThingTests.cs", src)]);

    private static IReadOnlyList<IBehaviour> Behaviours(string src = Corpus) =>
        new BehaviourResolver().Resolve(new CorpusParser().Parse(src, "app.beh")).Behaviours;

    // ── the counting, moved out of kit check ────────────────────────────────────

    [Fact]
    public void BEH_HEALTH_1_Covered_uncovered_and_pending_are_decided_from_the_mapping()
    {
        var counted = Counter.Count(Behaviours(), JsonNode.Parse(Mapping)!.AsObject(), Reading().Titles, "app.tests.json");

        Assert.Null(counted.Refusal);
        Assert.Equal(["BEH-1"], counted.Covered.Select(b => b.Id));
        Assert.Equal(["BEH-2"], counted.Uncovered.Select(b => b.Id));
        Assert.Equal(["BEH-3"], counted.Pending.Select(b => b.Id));
        Assert.Empty(counted.Errors);
        Assert.Equal(["ThingTests.cs"], counted.Linked["BEH-1"]);
    }

    [Fact]
    public void A_pending_behaviour_a_test_names_is_covered_and_not_pending()
    {
        var map = JsonNode.Parse("""{"BEH-3": [{"file": "ThingTests.cs", "title": "It_works"}]}""")!.AsObject();

        var counted = Counter.Count(Behaviours(), map, Reading().Titles, "app.tests.json");

        Assert.Equal(["BEH-3"], counted.Covered.Select(b => b.Id));
        Assert.Empty(counted.Pending);
    }

    [Fact]
    public void A_mapping_entry_that_names_no_test_rots_loudly_and_covers_nothing()
    {
        var map = JsonNode.Parse("""{"BEH-1": [{"file": "ThingTests.cs", "title": "Renamed"}], "BEH-9": []}""")!.AsObject();

        var counted = Counter.Count(Behaviours(), map, Reading().Titles, "app.tests.json");

        Assert.Empty(counted.Covered);
        Assert.Equal(2, counted.Errors.Count);
        Assert.Contains("renamed or deleted?", counted.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void A_mapping_that_is_not_an_array_per_behaviour_is_a_refusal()
    {
        var map = JsonNode.Parse("""{"BEH-1": "ThingTests.cs"}""")!.AsObject();

        var counted = Counter.Count(Behaviours(), map, Reading().Titles, "app.tests.json");

        Assert.Equal("app.tests.json: BEH-1 must be an array of { file, title }", counted.Refusal);
    }

    [Theory]
    [InlineData("backend/Balenthiran.Kit.Tests/ThingTests.cs", true)]
    [InlineData("frontend/e2e/editor.spec.ts", true)]
    [InlineData("ui/src/a.test.tsx", true)]
    [InlineData("README.md", false)]
    [InlineData("frontend/node_modules/dep/dep.spec.ts", false)]
    [InlineData("backend/bin/Debug/ThingTests.cs", false)]
    [InlineData("dist/a.test.js", false)]
    public void Which_paths_are_test_files_is_decided_in_one_place(string path, bool expected)
    {
        Assert.Equal(expected, Counter.IsTestPath(path));
    }

    [Fact]
    public void Titles_whose_two_counts_disagree_are_a_refusal_that_stops_the_read()
    {
        var read = 0;
        var files = new[] { ("a.spec.ts", "test(name, () => {});\n"), ("b.spec.ts", "test('b', () => {});\n") }
            .Select(f => { read++; return f; });

        var reading = Counter.ReadTitles(files);

        Assert.Contains("a.spec.ts: read 0 test(s) but 1 test declaration(s)", reading.Refusal, StringComparison.Ordinal);
        Assert.Equal(1, read);
    }

    // ── # kit:repo ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("# kit:repo o/r@dev\nbehaviour BEH-1 \"a\"\n", "o/r@dev")]
    [InlineData("# a header\n#   kit:repo   jemmy8oy-northstar/kit@feat/x  \n", "jemmy8oy-northstar/kit@feat/x")]
    [InlineData("# kit:repo o/r@dev\r\nbehaviour BEH-1 \"a\"\r\n", "o/r@dev")]
    public void A_kit_repo_line_names_where_the_tests_live(string src, string repo)
    {
        Assert.Equal((repo, null), RepoDirective.Read(src));
    }

    [Theory]
    [InlineData("behaviour BEH-1 \"a\"\n")]
    [InlineData("# see kit:repo for where tests live\n")]
    [InlineData("# a comment that mentions # kit:repo mid-line\n")]
    [InlineData("# kit:repository o/r@dev\n")]
    public void Only_the_directive_itself_names_a_repo(string src)
    {
        Assert.Equal((null, null), RepoDirective.Read(src));
    }

    [Theory]
    [InlineData("# kit:repo\n")]
    [InlineData("# kit:repo o/r\n")]
    [InlineData("# kit:repo o@dev\n")]
    public void A_kit_repo_line_that_is_not_owner_repo_at_branch_is_said_not_skipped(string src)
    {
        var (repo, problem) = RepoDirective.Read(src);

        Assert.Null(repo);
        Assert.Contains("owner/repo@branch", problem, StringComparison.Ordinal);
    }

    // ── the project view ────────────────────────────────────────────────────────

    private sealed class Store(ITestReading? reading = null, string? error = null) : ICoverageStore
    {
        public ITestReading? Reading(string repo) => repo == Repo ? reading : null;

        public string? Error(string repo) => repo == Repo ? error : null;
    }

    private static ProjectViewer Viewer(string corpus, string? mapping, ICoverageStore? store)
    {
        var dir = Directory.CreateTempSubdirectory("kit-coverage-").FullName;
        File.WriteAllText(Path.Combine(dir, "app.beh"), corpus);
        if (mapping is not null)
        {
            File.WriteAllText(Path.Combine(dir, "app.tests.json"), mapping);
        }

        return new ProjectViewer(new CorpusDirectory(dir, RepoLayout.Root), new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter(), store);
    }

    private static string Reason(ProjectViewer viewer)
    {
        var coverage = Assert.IsType<UnavailableCoverage>(((ProjectView)viewer.View("app")).Coverage);
        Assert.False(coverage.Available);
        return coverage.Reason;
    }

    [Fact]
    public void BEH_HEALTH_1_A_seeded_store_gives_the_view_ids_and_the_list_counts()
    {
        var viewer = Viewer(Corpus, Mapping, new Store(Reading()));

        var detail = Assert.IsType<AvailableCoverage>(((ProjectView)viewer.View("app")).Coverage);
        var row = ((ProjectSummary)viewer.Summary("app")).Coverage;

        Assert.True(detail.Available);
        Assert.Equal(["BEH-1"], detail.Covered);
        Assert.Equal(["BEH-2"], detail.Uncovered);
        Assert.Equal((true, 1, 1, null), (row.Available, row.Covered, row.Uncovered, row.Reason));
    }

    [Fact]
    public void BEH_HEALTH_1_The_view_serialises_the_shapes_the_ui_reads()
    {
        var serialiser = new EngineJsonSerialiser();

        var measured = JsonNode.Parse(serialiser.Serialise((ProjectView)Viewer(Corpus, Mapping, new Store(Reading())).View("app")))!["coverage"]!;
        var unmeasured = JsonNode.Parse(serialiser.Serialise((ProjectView)Viewer(Corpus, Mapping, new Store()).View("app")))!["coverage"]!;
        var row = JsonNode.Parse(serialiser.Serialise(Viewer(Corpus, Mapping, new Store(Reading())).Summary("app")))!["coverage"]!;

        Assert.Equal("""{"available":true,"covered":["BEH-1"],"uncovered":["BEH-2"]}""", measured.ToJsonString());
        Assert.Equal("""{"available":false,"reason":"not read yet"}""", unmeasured.ToJsonString());
        Assert.Equal("""{"available":true,"covered":1,"uncovered":1}""", row.ToJsonString());
    }

    [Fact]
    public void Local_kit_has_no_store_and_keeps_saying_no_repo_was_given()
    {
        Assert.Equal("no --repo given, so no test files were read", Reason(Viewer(Corpus, Mapping, null)));
    }

    [Fact]
    public void An_app_with_no_kit_repo_line_says_so()
    {
        var corpus = Corpus.Replace("# kit:repo o/r@dev\n", string.Empty, StringComparison.Ordinal);

        Assert.Equal("no `# kit:repo` line names where this app's tests live", Reason(Viewer(corpus, Mapping, new Store(Reading()))));
    }

    [Fact]
    public void A_malformed_kit_repo_line_says_what_it_wants()
    {
        var corpus = Corpus.Replace("o/r@dev", "o/r", StringComparison.Ordinal);

        Assert.Contains("owner/repo@branch", Reason(Viewer(corpus, Mapping, new Store(Reading()))), StringComparison.Ordinal);
    }

    [Fact]
    public void An_app_with_no_tests_mapping_says_so()
    {
        Assert.Equal("no app.tests.json mapping", Reason(Viewer(Corpus, null, new Store(Reading()))));
    }

    [Fact]
    public void Before_the_first_read_lands_the_reason_is_not_read_yet()
    {
        Assert.Equal("not read yet", Reason(Viewer(Corpus, Mapping, new Store())));
    }

    [Fact]
    public void A_failed_first_read_is_the_reason_when_there_is_nothing_older()
    {
        Assert.Equal("o/r@dev: GitHub answered 502", Reason(Viewer(Corpus, Mapping, new Store(error: "o/r@dev: GitHub answered 502"))));
    }

    [Fact]
    public void A_failed_later_read_keeps_serving_the_last_good_number()
    {
        var viewer = Viewer(Corpus, Mapping, new Store(Reading(), "o/r@dev: GitHub answered 502"));

        Assert.IsType<AvailableCoverage>(((ProjectView)viewer.View("app")).Coverage);
    }

    [Fact]
    public void A_reading_the_reader_refused_is_the_reason_not_a_number()
    {
        var refused = Counter.ReadTitles([("a.spec.ts", "test(name, () => {});\n")]);

        Assert.Contains("the two counts disagree", Reason(Viewer(Corpus, Mapping, new Store(refused))), StringComparison.Ordinal);
    }

    [Fact]
    public void A_mapping_that_is_not_a_json_object_is_said_not_thrown()
    {
        Assert.Equal("app.tests.json is not a JSON object", Reason(Viewer(Corpus, "[1]", new Store(Reading()))));
    }

    [Fact]
    public void A_mapping_the_counter_refuses_is_the_reason()
    {
        Assert.Equal("app.tests.json: BEH-1 must be an array of { file, title }", Reason(Viewer(Corpus, """{"BEH-1": 1}""", new Store(Reading()))));
    }

    // ── the store ───────────────────────────────────────────────────────────────

    private sealed class Tests_(Func<string, ITestReading?> read) : IGitHubTestReader
    {
        public List<string> Asked { get; } = [];

        public Task<ITestReading?> ReadTestsAsync(string repo, ICoverageCounter counter, CancellationToken cancellationToken = default)
        {
            Asked.Add(repo);
            return Task.FromResult(read(repo));
        }
    }

    private static CoverageStore StoreOver(Tests_ reader, params (string App, string Text)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("kit-coverage-store-").FullName;
        foreach (var (app, text) in files)
        {
            File.WriteAllText(Path.Combine(dir, app + ".beh"), text);
        }

        return new CoverageStore(new CorpusDirectory(dir, RepoLayout.Root), reader, Counter);
    }

    [Fact]
    public async Task The_store_reads_each_named_repo_once_however_many_apps_share_it()
    {
        var reader = new Tests_(_ => Reading());
        var store = StoreOver(reader, ("a", Corpus), ("b", Corpus), ("c", "behaviour BEH-1 \"no line\"\n"), ("d", Corpus.Replace("o/r", "o/other", StringComparison.Ordinal)));

        await store.RefreshAsync();

        Assert.Equal(["o/other@dev", "o/r@dev"], reader.Asked);
        Assert.NotNull(store.Reading("o/r@dev"));
        Assert.Null(store.Reading("o/nowhere@dev"));
    }

    [Fact]
    public async Task A_failed_read_keeps_the_last_good_reading_and_records_why()
    {
        var fail = false;
        var reader = new Tests_(_ => fail ? throw new GitHubReadException("o/r@dev: GitHub answered 502") : Reading());
        var store = StoreOver(reader, ("a", Corpus));
        await store.RefreshAsync();
        var good = store.Reading(Repo);

        fail = true;
        await store.RefreshAsync();

        Assert.Same(good, store.Reading(Repo));
        Assert.Equal("o/r@dev: GitHub answered 502", store.Error(Repo));

        fail = false;
        await store.RefreshAsync();
        Assert.Null(store.Error(Repo));
    }

    [Fact]
    public async Task An_unchanged_tree_keeps_the_reading_it_has()
    {
        var first = true;
        var reader = new Tests_(_ => first ? Reading() : null);
        var store = StoreOver(reader, ("a", Corpus));
        await store.RefreshAsync();
        var kept = store.Reading(Repo);

        first = false;
        await store.RefreshAsync();

        Assert.Same(kept, store.Reading(Repo));
        Assert.Null(store.Error(Repo));
    }

    [Fact]
    public async Task One_repo_that_cannot_be_read_does_not_stop_the_others()
    {
        var reader = new Tests_(repo => repo == "o/a@dev" ? throw new GitHubReadException("down") : Reading());
        var store = StoreOver(reader, ("a", Corpus.Replace("o/r", "o/a", StringComparison.Ordinal)), ("b", Corpus.Replace("o/r", "o/b", StringComparison.Ordinal)));

        await store.RefreshAsync();

        Assert.Equal("down", store.Error("o/a@dev"));
        Assert.NotNull(store.Reading("o/b@dev"));
    }
}
